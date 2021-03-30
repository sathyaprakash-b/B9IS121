using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Threading;

namespace SignalRChat.Hubs
{

    class HubMethods
    {
        private IHubContext<ChatHub> _hubContext;
        public HubMethods(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task WriteMessageAsync(string method, string user, string message)
        {
            return _hubContext.Clients.All.SendAsync(method, user, message);
        }
    }

    public class Auction
    {
        public int AdId { get; set; }
        public int InitialPrice { get; set; }
        public int CurrentPrice { get; set; }
        public int TotalDuration { get; set; }
        public int TimeRemaining { get; set; }
        public int CountDown { get; set; }

        public DateTime StartTime { get; set; }
        public List<Bid> BidList { get; set; }
    }

    public class AuctionMessage
    {
        public string Method { get; set; }
        /* public string User { get; set; } */
    }
    public class GetAuctionRequest : AuctionMessage
    {

    }

    public class GetAuctionResponse : AuctionMessage
    {
        public int AdId { get; set; }
        public int InitialPrice { get; set; }
        public int CurrentPrice { get; set; }
        public int TotalDuration { get; set; }
        public int TimeRemaining { get; set; }
        public int CountDown { get; set; }
        public List<Bid> BidList { get; set; }
    }

    public class Bid
    {
        public string user { get; set; }
        public int price { get; set; }
        public int bidTime { get; set; }
    }

    public class StartAuctionRequest : AuctionMessage
    {
        public int AdId { get; set; }
        public int InitialPrice { get; set; }
        public int TotalDuration { get; set; }
        public int CountDown { get; set; }
    }

    public class StartAuctionResponse : AuctionMessage
    {
        public int AdId { get; set; }
        public int TotalDuration { get; set; }
        public int CountDown { get; set; }
    }

    public class BidRequest : AuctionMessage
    {
        public int AdId { get; set; }
        public int Price { get; set; }
    }

    public class BidResponse : AuctionMessage
    {
        public int AdId { get; set; }
        public string Result { get; set; }
    }

    public class EndAuction : AuctionMessage
    {
        public int AdId { get; set; }
        public string user { get; set; }
        public int price { get; set; }
    }

    public class CancelAuction : AuctionMessage
    {
        public int AdId { get; set; }
    }


public class ChatHub : Hub
    {
        private HubMethods _hubMethods;
        private static Auction CurrentAuction = null;
        private static Hashtable ConIds = new Hashtable(20);
        static private Timer AuctionTimer, BidTimer;
        public static int BidInterval = 10000;

        public ChatHub(IHubContext<ChatHub> hubContext) => _hubMethods = new HubMethods(hubContext);

        public void RegisterConId(string UserID)
        {
            if (ConIds.ContainsKey(UserID))
                ConIds[UserID] = Context.ConnectionId;
            else
                ConIds.Add(UserID, Context.ConnectionId);
        }

        private async void SendAuctionResponse()
        {
            GetAuctionResponse resp = new GetAuctionResponse
            {
                Method = "GetAuctionResponse",
                AdId = 0
            };
            if (CurrentAuction != null)
            {
                resp.AdId = CurrentAuction.AdId;
                resp.CountDown = CurrentAuction.CountDown;
                resp.BidList = CurrentAuction.BidList;
                resp.CurrentPrice = CurrentAuction.CurrentPrice;
                resp.InitialPrice = CurrentAuction.InitialPrice;
                resp.TotalDuration = CurrentAuction.TotalDuration;
                resp.TimeRemaining = Convert.ToInt32(CurrentAuction.StartTime.Subtract(DateTime.UtcNow).TotalSeconds);
            }
            string respStr = JsonSerializer.Serialize<GetAuctionResponse>(resp);
            await Clients.All.SendAsync("ReceiveMessage", "Admin", respStr);
        }

        private async void BidTimerExpired(object state)
        {
            EndAuction msg = new EndAuction
            {
                Method = "EndAuction",
                AdId = 0,
                price = 0
            };
            if (CurrentAuction != null)
            {
                msg.AdId = CurrentAuction.AdId;
                int length = CurrentAuction.BidList.Count;
                if (length > 0)
                {
                    Bid item = CurrentAuction.BidList[length - 1];
                    msg.price = item.price;
                    msg.user = item.user;
                }
            }
            string msgStr = JsonSerializer.Serialize<EndAuction>(msg);
            //await Clients.All.SendAsync("ReceiveMessage", "Admin", msgStr);
            await _hubMethods.WriteMessageAsync("ReceiveMessage", "Admin", msgStr);
            BidTimer.Change(Timeout.Infinite, Timeout.Infinite);
            AuctionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            CurrentAuction = null;
        }

        private async void AuctionTimerExpired(object state)
        {
            EndAuction msg = new EndAuction
            {
                Method = "EndAuction",
                AdId = 0,
            };
            if (CurrentAuction != null)
            {
                msg.AdId = CurrentAuction.AdId;
                int length = CurrentAuction.BidList.Count;
                if (length > 0)
                {
                    Bid item = CurrentAuction.BidList[length - 1];
                    msg.price = item.price;
                    msg.user = item.user;
                }
            }
            string msgStr = JsonSerializer.Serialize<EndAuction>(msg);
            //await Clients.All.SendAsync("ReceiveMessage", "Admin", msgStr);
            await _hubMethods.WriteMessageAsync("ReceiveMessage", "Admin", msgStr);
            //BidTimer.Change(Timeout.Infinite, Timeout.Infinite);
            AuctionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            CurrentAuction = null;
        }

        public async Task SendMessage(string user, string message)
        {
            string input = message;
            AuctionMessage msg;
            try
            {
                msg = JsonSerializer.Deserialize<AuctionMessage>(input);
            }catch(Exception e)
            {
                msg = null;
            }
            if (msg != null)
            {
                switch (msg.Method)
                {
                    case "GetAuctionRequest":
                        GetAuctionResponse response = new GetAuctionResponse
                        {
                            Method = "GetAuctionResponse",
                            AdId = 0,
                        };
                        if(CurrentAuction != null)
                        {
                            response.AdId = CurrentAuction.AdId;
                            response.CountDown = CurrentAuction.CountDown;
                            response.BidList = CurrentAuction.BidList;
                            response.CurrentPrice = CurrentAuction.CurrentPrice;
                            response.InitialPrice = CurrentAuction.InitialPrice;
                            response.TotalDuration = CurrentAuction.TotalDuration;
                            response.TimeRemaining = Convert.ToInt32(CurrentAuction.StartTime.Subtract(DateTime.UtcNow).TotalSeconds);
                        }
                        string resStr = JsonSerializer.Serialize<GetAuctionResponse>(response);
                        await Clients.Client((string)ConIds[user]).SendAsync("ReceiveMessage", user, resStr);
                        break;

                    case "StartAuctionRequest":
                        StartAuctionRequest request;
                        try
                        {
                            request = JsonSerializer.Deserialize<StartAuctionRequest>(input);
                        }
                        catch (Exception e)
                        {
                            request = null;
                        }
                        if (request != null)
                        {
                            if (CurrentAuction == null)
                            {
                                CurrentAuction = new Auction();
                                CurrentAuction.BidList = new List<Bid>();
                                CurrentAuction.AdId = request.AdId;
                                CurrentAuction.CountDown = request.CountDown;
                                CurrentAuction.InitialPrice = request.InitialPrice;
                                CurrentAuction.CurrentPrice = request.InitialPrice;
                                CurrentAuction.TotalDuration = request.TotalDuration;
                                CurrentAuction.TimeRemaining = request.TotalDuration;
                                CurrentAuction.StartTime = DateTime.UtcNow.AddSeconds(Convert.ToDouble(request.TotalDuration));
                                AuctionTimer = new System.Threading.Timer(AuctionTimerExpired, null,
                                    request.TotalDuration * 1000, 0);
                                //BidTimer = new System.Threading.Timer(BidTimerExpired, null,
                                //    BidInterval, 0);
                            }
 
                        }
                        SendAuctionResponse();
                        break;

                    case "BidRequest":
                        BidRequest bidRequest;
                        try
                        {
                           bidRequest = JsonSerializer.Deserialize<BidRequest>(input);
                        }
                        catch (Exception e)
                        {
                            bidRequest = null;
                        }
                        if(bidRequest != null)
                        {
                            if (CurrentAuction.CurrentPrice < bidRequest.Price)
                            {
                                CurrentAuction.CurrentPrice = bidRequest.Price;
                                Bid item = new Bid();
                                item.user = user;
                                item.price = bidRequest.Price;
                                int timeRemaining = Convert.ToInt32(CurrentAuction.StartTime.Subtract(DateTime.UtcNow).TotalSeconds);
                                item.bidTime = CurrentAuction.TotalDuration - timeRemaining;
                                CurrentAuction.BidList.Add(item);
                                //BidTimer.Change(BidInterval, 0);
                            }
                        }
                        SendAuctionResponse();
                        break;

                    default:
                        break;
                }
            }
            else
            {
                await Clients.All.SendAsync("ReceiveMessage", user, message);
            }
/*
            if (msg != null && msg.Action == "GetAuction")
            {
                //GetAuction getAucObj = (GetAuction)msg;
                GetAuction getAucObj = JsonSerializer.Deserialize<GetAuction>(message);
                await Clients.All.SendAsync("ReceiveMessage", user, getAucObj.CurrentAuction);
            }
            else
            {
                await Clients.All.SendAsync("ReceiveMessage", user, message);
            }
*/
        }
    }
}
