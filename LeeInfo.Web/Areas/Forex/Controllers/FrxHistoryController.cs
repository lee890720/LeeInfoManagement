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
    public class FrxHistoryController : Controller
    {
        private readonly AppIdentityDbContext _identitycontext;
        private UserManager<AppIdentityUser> _userManager;
        private readonly AppDbContext _context;

        public FrxHistoryController(AppIdentityDbContext identitycontext, UserManager<AppIdentityUser> usermgr, AppDbContext context)
        {
            _identitycontext = identitycontext;
            _userManager = usermgr;
            _context = context;
        }
        public async Task<IActionResult> Index(int? acId)
        {
            #region Parameters
            string _accessToken = "";
            string _apiUrl = "https://api.spotware.com/";
            AppIdentityUser _user = await _userManager.FindByNameAsync(User.Identity.Name);
            _accessToken = _user.AccessToken;
            #endregion
            #region GetAccount
            var useraccounts = _identitycontext.AspNetUserForexAccount.Where(u => u.AppIdentityUserId == _user.Id).ToList();
            //var useraccounts = _user.AspNetUserForexAccount;
            var temp = _context.FrxAccount.Where(x => useraccounts.SingleOrDefault(s => s.AccountNumber == x.AccountNumber && s.Password == x.Password) != null).ToList();
            if (temp.Count == 0)
                return Redirect("/");
            var accounts = TradingAccount.GetTradingAccounts(_apiUrl, _accessToken);
            foreach (var a in accounts)
            {
                var temp_ac = temp.SingleOrDefault(x => x.AccountNumber == a.AccountNumber);
                if (temp_ac != null)
                {
                    temp_ac.Balance = a.Balance / 100;
                    temp_ac.BrokerName = a.BrokerTitle;
                    temp_ac.Currency = a.DepositCurrency;
                    temp_ac.IsLive = a.Live;
                    temp_ac.PreciseLeverage = a.Leverage;
                    temp_ac.TraderRegistrationTime = ConvertJson.StampToDateTime(a.TraderRegistrationTimestamp);
                    _context.Update(temp_ac);
                    await _context.SaveChangesAsync();
                }
            }
            var frxaccounts = _context.FrxAccount.Where(x => useraccounts.SingleOrDefault(s => s.AccountNumber == x.AccountNumber && s.Password == x.Password) != null).ToList();
            var frxaccount = new FrxAccount();
            if (acId == null)
            {
                var tempuserac = useraccounts.SingleOrDefault(x => x.Alive == true);
                if (tempuserac == null)
                    tempuserac = useraccounts[0];
                frxaccount = frxaccounts.SingleOrDefault(x => x.AccountNumber == tempuserac.AccountNumber && x.Password == tempuserac.Password);
            }
            else
                frxaccount = frxaccounts.SingleOrDefault(x => x.AccountId == acId);
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
                fp.Margin = fp.MarginRate * fp.Volume / frxaccount.PreciseLeverage;
                _context.Add(fp);
                await _context.SaveChangesAsync();
            }
            var frxpositions = _context.FrxPosition.Where(x => x.AccountId == frxaccount.AccountId);
            #endregion
            #region UpdateAccount
            double unrnet = 0;
            double marginused = 0;
            foreach (var p in frxpositions)
            {
                unrnet += p.NetProfit;
                marginused += p.Margin;
            }
            frxaccount.Equity = frxaccount.Balance + unrnet;
            frxaccount.UnrealizedNetProfit = unrnet;
            frxaccount.MarginUsed = Math.Round(marginused, 2);
            frxaccount.FreeMargin = Math.Round(frxaccount.Equity - marginused, 2);
            frxaccount.MarginLevel = Math.Round(frxaccount.Equity / marginused * 100, 2);
            _context.Update(frxaccount);
            await _context.SaveChangesAsync();
            #endregion
            #region GetHistory
            DateTime utcnow = DateTime.UtcNow;
            string fromtimestamp = ConvertJson.DateTimeToStamp(frxaccount.TraderRegistrationTime);
            string totimestamp = ConvertJson.DateTimeToStamp(utcnow);
            var deal = Deal.GetDeals(_apiUrl, frxaccount.AccountId.ToString(), _accessToken, fromtimestamp, totimestamp);
            var deal_history = new List<Deal>();
            foreach (var d in deal)
            {
                if (d.PositionCloseDetails != null)
                    deal_history.Add(d);
            }
            foreach (var h in deal_history)
            {
                FrxHistory fh = new FrxHistory();
                fh.ClosingDealId = h.DealID;
                fh.AccountId = frxaccount.AccountId;
                fh.Balance = h.PositionCloseDetails.Balance / 100;
                fh.BalanceVersion = h.PositionCloseDetails.BalanceVersion;
                fh.BaseToUSDConversionRate = h.BaseToUSDConversionRate;
                fh.ClosedToDepoitConversionRate = h.PositionCloseDetails.ClosedToDepositConversionRate;
                fh.ClosingTime = ConvertJson.StampToDateTime(h.ExecutionTimestamp);
                fh.ClosingPrice = h.ExecutionPrice;
                fh.Comment = h.Comment;
                fh.Commissions = h.PositionCloseDetails.Commission / 100;
                fh.EntryPrice = h.PositionCloseDetails.EntryPrice;
                long tempstamp = System.Convert.ToInt64(h.ExecutionTimestamp);
                foreach (var d in deal)
                {
                    if (d.PositionID == h.PositionID)
                    {
                        if (System.Convert.ToInt64(d.ExecutionTimestamp) < tempstamp)
                            tempstamp = System.Convert.ToInt64(d.ExecutionTimestamp);
                    }
                }
                fh.EntryTime = ConvertJson.StampToDateTime(tempstamp.ToString());
                fh.Equity = h.PositionCloseDetails.Equity / 100;
                fh.EquityBaseRoi = h.PositionCloseDetails.EquityBasedRoi / 100;
                fh.GrossProfit = h.PositionCloseDetails.Profit / 100;
                fh.Label = h.Label;
                fh.MarginRate = h.MarginRate;
                fh.Swap = h.PositionCloseDetails.Swap / 100;
                fh.NetProfit = fh.GrossProfit + fh.Swap + fh.Commissions;
                fh.Pips = h.PositionCloseDetails.ProfitInPips;
                fh.PositionId = h.PositionID;
                fh.SymbolCode = h.SymbolName;
                fh.Volume = h.PositionCloseDetails.ClosedVolume / 100;

                var tempvolume = Convert.ToDouble(fh.Volume);
                double tempsub = 100000;
                if (fh.SymbolCode == "XBRUSD" || fh.SymbolCode == "XTIUSD")
                    tempsub = 100;
                if (fh.SymbolCode == "XAGUSD" || fh.SymbolCode == "XAGEUR")
                    tempsub = 1000;
                if (fh.SymbolCode == "XAUUSD" || fh.SymbolCode == "XAUEUR")
                    tempsub = 100;
                fh.Quantity = tempvolume / tempsub;
                fh.QuoteToDepositConversionRate = h.PositionCloseDetails.QuoteToDepositConversionRate;
                fh.Roi = h.PositionCloseDetails.Roi;
                fh.TradeType = h.TradeSide == "BUY" ? TradeType.Sell : TradeType.Buy;
                var result = _context.FrxHistory.Find(fh.ClosingDealId);
                if (result == null)
                {
                    _context.Add(fh);
                    await _context.SaveChangesAsync();
                }
            }
            var frxhistories = _context.FrxHistory.Where(x => x.AccountId == frxaccount.AccountId);
            #endregion

            return View(Tuple.Create<FrxAccount, List<FrxHistory>, List<FrxAccount>>(frxaccount, frxhistories.ToList(), frxaccounts.ToList()));
        }

        public JsonResult GetHistory(int? acId)
        {
            var data = _context.FrxHistory.Where(x => x.AccountId == acId).ToList();
            return Json(new { data, data.Count });
        }

        public JsonResult GetEarnings(int? acId)
        {
            var frxhistories = _context.FrxHistory.Where(x => x.AccountId == acId).OrderBy(x => x.ClosingTime).ToList();
            var days = (DateTime.UtcNow - frxhistories[0].ClosingTime).Days;
            var data =new List<Earnings>();
            for(int i=1;i<=days;i++)
            {
                var earnings = new Earnings();
                double net = 0;
                double qua = 0;
                var closingtime = frxhistories[0].ClosingTime.Date.AddDays(i);
                var temptime = closingtime.AddDays(-1);
                var tempid = 0;
                for(int f=0;f<frxhistories.Count;f++)
                {
                    if (closingtime <= frxhistories[f].ClosingTime)
                    {
                        tempid = frxhistories[f].ClosingDealId;
                        if(temptime> frxhistories[f].ClosingTime)
                        {
                            net += frxhistories[f].NetProfit;
                            qua += frxhistories[f].Quantity;
                        }
                    }
                    else
                        break;
                }
                var frxhistory = frxhistories.SingleOrDefault(x => x.ClosingDealId==tempid);
                if (frxhistory != null)
                {
                    earnings.Id = i;
                    earnings.AccountId = frxhistory.AccountId;
                    earnings.Balance = frxhistory.Balance;
                    earnings.Equity = frxhistory.Equity;
                    earnings.ClosingTime = temptime;
                    earnings.NetProfit = net;
                    earnings.Quantity = qua;
                    data.Add(earnings);
                }
            }
            return Json(new { data, data.Count });
        }
    }

    public class Earnings
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public double Balance { get; set; }
        public double Equity { get; set; }
        public DateTime ClosingTime { get; set; }
        public double NetProfit { get; set; }
        public double Quantity { get; set; }
    }
}
