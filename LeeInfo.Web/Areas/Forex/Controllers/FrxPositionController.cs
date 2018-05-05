﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Connect_API.Accounts;
using Connect_API.Trading;
using System.Security.Cryptography.X509Certificates;
using LeeInfo.Data;
using LeeInfo.Data.AppIdentity;
using Microsoft.AspNetCore.Identity;
using ChartJSCore.Models;
using ChartJSCore.Helpers;
using ChartJSCore.Plugins;
using LeeInfo.Lib;
using Microsoft.AspNetCore.Mvc.Rendering;
using LeeInfo.Data.Forex;
using System.Drawing;
using Microsoft.AspNetCore.Http;

namespace LeeInfo.Web.Areas.Forex.Controllers
{
    [Area("Forex")]
    [Authorize(Roles = "Admins,Forex")]
    public class FrxPositionController : Controller
    {
        private readonly AppDbContext _identitycontext;
        UserManager<AppIdentityUser> _userManager;
        private readonly AppDbContext _context;

        public FrxPositionController(AppDbContext identitycontext, UserManager<AppIdentityUser> usermgr, AppDbContext context)
        {
            _identitycontext = identitycontext;
            _userManager = usermgr;
            _context = context;
        }
        public async Task<IActionResult> Index(string acNum=null)
        {
            #region Parameters
            string _clientId = "";
            string _clientSecret = "";
            string _accessToken = "";
            string _refreshToken = "";
            string _apiUrl = "https://api.spotware.com/";
            AppIdentityUser _user = await _userManager.FindByNameAsync(User.Identity.Name);
            _clientId = _user.ClientId;
            _clientSecret = _user.ClientSecret;
            _accessToken = _user.AccessToken;
            _refreshToken = _user.RefreshToken;
            #endregion
            #region GetAccount
            var accounts = TradingAccount.GetTradingAccounts(_apiUrl, _accessToken);
            var temp = _context.FrxAccount.Where(x => x.UserName == User.Identity.Name);
            _context.RemoveRange(temp);
            await _context.SaveChangesAsync();
            foreach (var a in accounts)
            {
                var sql_accounts = _context.FrxAccount.Where(x => x.AccountId == Convert.ToInt32(a.AccountId));
                if (sql_accounts.Count() == 0)
                {
                    FrxAccount fa = new FrxAccount();
                    fa.AccountId = a.AccountId;
                    fa.AccountNumber = a.AccountNumber;
                    fa.Balance = a.Balance / 100;
                    fa.BrokerName = a.BrokerTitle;
                    fa.Currency = a.DepositCurrency;
                    fa.IsLive = a.Live;
                    fa.PreciseLeverage = a.Leverage;
                    fa.TraderRegistrationTime = ConvertJson.StampToDateTime(a.TraderRegistrationTimestamp);
                    fa.UserName = User.Identity.Name;
                    _context.Add(fa);
                    await _context.SaveChangesAsync();
                }
            }
            var frxaccounts = _context.FrxAccount.Where(x=>x.UserName==_user.UserName);
            var frxaccount = new FrxAccount();
            var vd_AccountNumber = acNum;
            if (string.IsNullOrEmpty(vd_AccountNumber))
            {
                frxaccount = frxaccounts.FirstOrDefault(x => x.IsLive == true);
            }
            else
            {
                frxaccount = frxaccounts.SingleOrDefault(x => x.AccountNumber == Convert.ToInt32(vd_AccountNumber));
            }
            #endregion
            #region GetPosition
            var temppositions = _context.FrxPosition.Where(x => x.AccountId == frxaccount.AccountId);
            _context.RemoveRange(temppositions);
            await _context.SaveChangesAsync();
            var position = Position.GetPositions(_apiUrl, frxaccount.AccountId.ToString(), _accessToken);
            foreach (var p in position)
            {
                FrxPosition fp = new FrxPosition();
                fp.Id = p.PositionID;
                fp.AccountId = frxaccount.AccountId;
                fp.Channel = p.Channel;
                fp.Comment = p.Comment;
                fp.Commissions = p.Commission * 2 / 100;
                fp.CurrentPrice = p.CurrentPrice;
                fp.EntryPrice = p.EntryPrice;
                fp.EntryTime = ConvertJson.StampToDateTime(p.EntryTimestamp);
                fp.GrossProfit = p.Profit / 100;
                fp.Label = p.Label;
                fp.MarginRate = p.MarginRate;
                fp.Swap = p.Swap / 100;
                fp.NetProfit = fp.GrossProfit + fp.Swap + fp.Commissions;
                fp.Pips = p.ProfitInPips;
                fp.Volume = p.Volume / 100;

                var tempvolume = Convert.ToDouble(fp.Volume);
                double tempsub = 100000;
                if (fp.SymbolCode == "XBRUSD" || fp.SymbolCode == "XTIUSD")
                    tempsub = 100;
                if (fp.SymbolCode == "XAGUSD" || fp.SymbolCode == "XAGEUR")
                    tempsub = 1000;
                if (fp.SymbolCode == "XAUUSD" || fp.SymbolCode == "XAUEUR")
                    tempsub = 100;
                fp.Quantity = tempvolume / tempsub;
                fp.StopLoss = p.StopLoss;
                fp.TakeProfit = p.TakeProfit;
                fp.SymbolCode = p.SymbolName;
                fp.TradeType = p.TradeSide == "BUY" ? TradeType.Buy : TradeType.Sell;
                fp.Margin = fp.MarginRate * fp.Volume /frxaccount.PreciseLeverage;
                _context.Add(fp);
                await _context.SaveChangesAsync();
            }
            var frxpositions = _context.FrxPosition.Where(x => x.AccountId == frxaccount.AccountId);
            #endregion
            #region UpdateAccount
            double unrnet = 0;
            double marginused = 0;
            foreach(var p in frxpositions)
            {
                unrnet += p.NetProfit;
                marginused += p.Margin;
            }
            frxaccount.Equity = frxaccount.Balance+unrnet;
            frxaccount.UnrealizedNetProfit =unrnet ;
            frxaccount.MarginUsed = Math.Round(marginused,2);
            frxaccount.FreeMargin = Math.Round(frxaccount.Equity - marginused,2);
            frxaccount.MarginLevel = Math.Round(frxaccount.Equity / marginused*100,2);
            _context.Update(frxaccount);
            await _context.SaveChangesAsync();
            #endregion
            #region GetPosGroup
            List<PosGroup> poss = new List<PosGroup>();
            foreach (var p in frxpositions)
            {
                var temp_poss = poss.Where(x => x.SymbolCode == p.SymbolCode && x.TradeType == p.TradeType);
                if (temp_poss.Count() == 0)
                {
                    var pos = new PosGroup();
                    pos.SymbolCode = p.SymbolCode;
                    pos.TradeType = p.TradeType;
                    pos.Quantity = p.Quantity;
                    pos.EntryPrice = p.EntryPrice;
                    pos.Swap = p.Swap;
                    pos.NetProfit = p.NetProfit;
                    pos.Pips = p.Pips;
                    pos.Gain = p.NetProfit / frxaccount.Balance;
                    poss.Add(pos);
                }
                else
                {
                    var pos = poss.First(x => x.SymbolCode == p.SymbolCode && x.TradeType == p.TradeType);
                    pos.EntryPrice = (pos.EntryPrice * pos.Quantity + p.EntryPrice * p.Quantity) / (pos.Quantity + p.Quantity);
                    pos.Pips = (pos.Pips * pos.Quantity + p.Pips * p.Quantity) / (pos.Quantity + p.Quantity);
                    pos.Quantity += p.Quantity;
                    pos.Swap += p.Swap;
                    pos.NetProfit += p.NetProfit;
                    pos.Gain = pos.NetProfit / frxaccount.Balance;
                }
            }
            #endregion
            //SetChart
            GeneratePieChart(poss);

            return View(Tuple.Create<FrxAccount,List<PosGroup>, List<FrxAccount>>(frxaccount,poss,frxaccounts.ToList()));
        }

        public JsonResult GetPosition()
        {
            var data = _context.FrxPosition.ToList();
            return Json(new { data, data.Count });
        }

        public JsonResult GetPosGroup()
        {
            var frxaccounts = _context.FrxAccount.Where(x => x.UserName == User.Identity.Name);
            var frxaccount = new FrxAccount();
            string vd_AccountNumber = (2017425).ToString();
            if (string.IsNullOrEmpty(vd_AccountNumber))
            {
                frxaccount = frxaccounts.FirstOrDefault(x => x.IsLive == true);
            }
            else
            {
                frxaccount = frxaccounts.SingleOrDefault(x => x.AccountNumber == Convert.ToInt32(vd_AccountNumber));
            }
            var frxpositions = _context.FrxPosition.Where(x => x.AccountId == frxaccount.AccountId);
            #region GetPosGroup
            List<PosGroup> poss = new List<PosGroup>();
            foreach (var p in frxpositions)
            {
                var temp_poss = poss.Where(x => x.SymbolCode == p.SymbolCode && x.TradeType == p.TradeType);
                if (temp_poss.Count() == 0)
                {
                    var pos = new PosGroup();
                    pos.SymbolCode = p.SymbolCode;
                    pos.TradeType = p.TradeType;
                    pos.Quantity = p.Quantity;
                    pos.EntryPrice = p.EntryPrice;
                    pos.Swap = p.Swap;
                    pos.NetProfit = p.NetProfit;
                    pos.Pips = p.Pips;
                    pos.Gain = p.NetProfit / frxaccount.Balance;
                    poss.Add(pos);
                }
                else
                {
                    var pos = poss.First(x => x.SymbolCode == p.SymbolCode && x.TradeType == p.TradeType);
                    pos.EntryPrice = (pos.EntryPrice * pos.Quantity + p.EntryPrice * p.Quantity) / (pos.Quantity + p.Quantity);
                    pos.Pips = (pos.Pips * pos.Quantity + p.Pips * p.Quantity) / (pos.Quantity + p.Quantity);
                    pos.Quantity += p.Quantity;
                    pos.Swap += p.Swap;
                    pos.NetProfit += p.NetProfit;
                    pos.Gain = pos.NetProfit / frxaccount.Balance;
                }
            }
            #endregion
            return Json(new { poss, poss.Count });
        }

        public void GeneratePieChart(List<PosGroup> posgroup)
        {
            List<string> lables = new List<string>();
            List<double> lots = new List<double>();
            List<string> colors = new List<string>();
            foreach(var a in posgroup)
            {
                lables.Add(a.SymbolCode);
                lots.Add(Math.Round(a.Quantity,2));
                colors.Add(Tools.GetRandomColor());
            }
            Chart chart = new Chart();
            chart.Type = "pie";          
            ChartJSCore.Models.Data data = new ChartJSCore.Models.Data();
            data.Labels = lables;

            PieDataset dataset = new PieDataset()
            {
                Label = "My dataset",
                BackgroundColor = colors,
                HoverBackgroundColor = colors,
                Data = lots
            };

            data.Datasets = new List<Dataset>();
            data.Datasets.Add(dataset);

            ChartJSCore.Models.Options options = new ChartJSCore.Models.Options();
            options.Legend = new Legend
            {
                Display = true,
                Position = "right"
            };

            chart.Data = data;
            chart.Options = options;

            ViewData["chart"] = chart;
        }
    }

    public class PosGroup
    {
        public string SymbolCode { get; set; }
        public TradeType TradeType { get; set; }
        public double Quantity { get; set; }
        public double EntryPrice { get; set; }
        public double Swap { get; set; }
        public double NetProfit { get; set; }
        public double Pips { get; set; }
        public double Gain { get; set; }
    }
}
