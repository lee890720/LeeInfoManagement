﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;

namespace Connect_API.Accounts
{
    /// <summary>
    /// The deal class is used for retrieving deals for a specified account using the Connect API RESTful web services.
    /// </summary>
    public class Deal
    {
        /// <summary>
        /// Gets or sets the deal identifier.
        /// </summary>
        /// <value>
        /// The deal identifier.
        /// </value>
        [JsonProperty("dealId")]
        public int DealId { get; set; }

        /// <summary>
        /// Gets or sets the position identifier.
        /// </summary>
        /// <value>
        /// The position identifier.
        /// </value>
        [JsonProperty("positionId")]
        public int PositionId { get; set; }

        /// <summary>
        /// Gets or sets the order identifier.
        /// </summary>
        /// <value>
        /// The order identifier.
        /// </value>
        [JsonProperty("orderId")]
        public int OrderId { get; set; }

        /// <summary>
        /// Gets or sets the trade side.
        /// </summary>
        /// <value>
        /// The trade side.
        /// </value>
        [JsonProperty("tradeSide")]
        public string TradeSide { get; set; }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        [JsonProperty("volume")]
        public long Volume { get; set; }

        /// <summary>
        /// Gets or sets the filled volume.
        /// </summary>
        /// <value>
        /// The filled volume.
        /// </value>
        [JsonProperty("filledVolume")]
        public long FilledVolume { get; set; }

        /// <summary>
        /// Gets or sets the name of the symbol.
        /// </summary>
        /// <value>
        /// The name of the symbol.
        /// </value>
        [JsonProperty("symbolName")]
        public string SymbolName { get; set; }

        /// <summary>
        /// Gets or sets the commision.
        /// </summary>
        /// <value>
        /// The commision.
        /// </value>
        [JsonProperty("commision")]
        public double Commision { get; set; }

        /// <summary>
        /// Gets or sets the execution price.
        /// </summary>
        /// <value>
        /// The execution price.
        /// </value>
        [JsonProperty("executionPrice")]
        public double ExecutionPrice { get; set; }

        /// <summary>
        /// Gets or sets the base to usd conversion rate.
        /// </summary>
        /// <value>
        /// The base to usd conversion rate.
        /// </value>
        [JsonProperty("baseToUsdConversionRate")]
        public double BaseToUSDConversionRate { get; set; }

        /// <summary>
        /// Gets or sets the margin rate.
        /// </summary>
        /// <value>
        /// The margin rate.
        /// </value>
        [JsonProperty("marginRate")]
        public double MarginRate { get; set; }

        /// <summary>
        /// Gets or sets the test partner.
        /// </summary>
        /// <value>
        /// The test partner.
        /// </value>
        [JsonProperty("TestPartner")]
        public string TestPartner { get; set; }

        /// <summary>
        /// Gets or sets the label.
        /// </summary>
        /// <value>
        /// The label.
        /// </value>
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the comment.
        /// </summary>
        /// <value>
        /// The comment.
        /// </value>
        [JsonProperty("comment")]
        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets the create timestamp.
        /// </summary>
        /// <value>
        /// The create timestamp.
        /// </value>
        [JsonProperty("createTimestamp")]
        public long CreateTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the execution timestamp.
        /// </summary>
        /// <value>
        /// The execution timestamp.
        /// </value>
        [JsonProperty("executionTimestamp")]
        public long ExecutionTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the position close details.
        /// </summary>
        /// <value>
        /// The position close details.
        /// </value>
        [JsonProperty("positionCloseDetails")]
        public PositionCloseDetails PositionCloseDetails { get; set; }
        /// <summary>
        /// Gets the deals.
        /// </summary>
        /// <param name="apiUrl">The API URL.</param>
        /// <param name="accountID">The account identifier.</param>
        /// <param name="accessToken">The access token.</param>
        /// <returns></returns>
        public static List<Deal> GetDeals(string apiUrl, string accountID, string accessToken, string fromTimeStamp, string toTimeStamp)
        {
            var client = new RestClient(apiUrl);
            var request = new RestRequest(@"connect/tradingaccounts/" + accountID + "/deals?oauth_token=" + accessToken + "&limit=750" + "&fromtimestamp=" + fromTimeStamp + "&totimestamp=" + toTimeStamp);
            var responseDeal = client.Execute<Deal>(request);
            var list = JsonConvert.DeserializeObject<List<Deal>>((JObject.Parse(responseDeal.Content)["data"]).ToString());
            JObject jo = JObject.Parse(responseDeal.Content);
            if (jo.Property("next") == null || jo.Property("next").ToString() == null)
                return list;
            var next = jo.Property("next").Value;
            for (int i = 0; i < 10000; i++)
            {
                var requestNext = new RestRequest(@"connect/tradingaccounts/" + accountID + "/deals?oauth_token=" + accessToken + "&limit=750" + "&cursor=" + next);
                var responseDealNext = client.Execute<Deal>(requestNext);
                list.AddRange(JsonConvert.DeserializeObject<List<Deal>>((JObject.Parse(responseDealNext.Content)["data"]).ToString()));
                JObject joNext = JObject.Parse(responseDealNext.Content);
                if (joNext.Property("next") == null || joNext.Property("next").ToString() == null)
                    return list;
                else
                    next = joNext.Property("next").Value;
            }
            return list;
        }
    }
}