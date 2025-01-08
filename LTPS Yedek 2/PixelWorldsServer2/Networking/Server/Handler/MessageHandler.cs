using FeatherNet;
using Kernys.Bson;
using PixelWorldsServer2.Database;
using PixelWorldsServer2.DataManagement;
using PixelWorldsServer2.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using Discord.WebSocket;
using System.Diagnostics;
using Discord.Net;
using Discord;
using Newtonsoft.Json;
using Discord.Webhook;
using System.Threading.Tasks;
using static PixelWorldsServer2.World.WorldSession;
using static PixelWorldsServer2.World.WorldInterface;
using static PixelWorldsServer2.Player;
using static FeatherNet.FeatherEvent;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using System.Numerics;
using System.Net.NetworkInformation;
using Discord.Commands;

namespace PixelWorldsServer2.Networking.Server
{
    public class MessageHandler
    {
        private PWServer pServer = null;

        public MessageHandler(PWServer pwServer)
        {
            pServer = pwServer;
        }
        private List<WorldSession> worlds = new List<WorldSession>();
        public List<WorldSession> GetWorlds() => worlds;

        private List<InventoryKey> itemList = new List<InventoryKey>();
        public List<InventoryKey> Items => itemList;

        private PlayerData pData;
        
       
        
        public void ProcessBSONPacket(FeatherClient client, BSONObject bObj)
        {
            if (pServer == null)
            {
                Util.Log("ERROR cannot process BSON packet when pServer is null!");
                return;
            }

            if (!bObj.ContainsKey("mc"))
            {
                Util.Log("Invalid bson packet (no mc!)");
                client.DisconnectLater();
                return; // Invalid Pixel Worlds BSON packet/!
            }

#if RELEASE
#endif
            int messageCount = bObj["mc"];


            Player p = client.data == null ? null : ((Player.PlayerData)client.data).player;
            for (int i = 0; i < messageCount; i++)
            {
                if (!bObj.ContainsKey($"m{i}"))
                    throw new Exception($"Non existing message object failed to be accessed by index '{i}'!");

                BSONObject mObj = bObj[$"m{i}"] as BSONObject;
                string mID = mObj[MsgLabels.MessageID];
                if (mObj["ID"].stringValue != "mP" && mObj["ID"].stringValue != "ST")
                    ReadBSON(mObj, Log: Util.LogClient);
                try
                {
                    switch (mID)
                    {

                        case MsgLabels.Ident.VersionCheck:
                            Util.Log("Client requests version check, responding now...");
                            //#endif
                            BSONObject resp = new BSONObject();
                            resp[MsgLabels.MessageID] = MsgLabels.Ident.VersionCheck;
                            resp[MsgLabels.VersionNumberKey] = pServer.Version;
                            client.Send(resp);
                            break;

                        case MsgLabels.Ident.GetPlayerData:
                            HandlePlayerLogon(client, mObj);
                            break;

                        case MsgLabels.Ident.TryToJoinWorld:
                            HandleTryToJoinWorld(p, mObj);
                            break;

                        case "TTJWR":
                            HandleTryToJoinWorldRandom(p);
                            break;

                        case "AHGetCCgy":
                            AHGetCCgy(p, mObj);
                            break;

                        case "PVi":
                            PVi(p, mObj);
                            break;


                        case "RDB":
                            HandleDailyBonus(p, mObj);
                            break;

                        case "GWotW":
                            WOFTW(p, mObj);
                            break;

                        case MsgLabels.Ident.GetWorld:
                            HandleGetWorld(p, mObj);
                            break;

                        case "GSb":
                            if (p != null)
                                p.isLoadingWorld = false;

                            p.Send(ref mObj);
                            break;

                        case "WCM":
                            HandleWorldChatMessage(p, mObj);
                            break;

                     

                        case "MWli":
                            HandleMoreWorldInfo(p, mObj);
                            break;

                        case "PSicU":
                            HandlePlayerStatusChange(p, mObj);
                            break;

                        case "BIPack":
                            HandleShopPurchase(p, mObj);
                            break;

                        case "RenamePlayer":
                            HandleRenamePlayer(p, mObj);
                            break;

                        case "QPi":
                            {
                                // Assuming "U" is the key for UserID in mObj
                                string userIdString = mObj["U"].stringValue;

                                // Assuming p.Data is the Player instance
                                Player user = pServer.GetOnlinePlayerByUserID(userIdString);

                                if (user != null)
                                {
                                    BSONObject packet = new BSONObject();
                                    packet["ID"] = "QPi";
                                    packet["TuID"] = p.Data.UserID;  // Assuming p.Data.UserID is already a string
                                    packet["QueryResult"] = new BSONArray() { @$"{p.Data.lvl}", 0, @"Player", @"1d", "0", 0, "LTPS", "test", "test", 10, "0", "0" };
                                    p.Send(ref packet);
                                }
                            }
                            break;

                        case "rOP": // request other players
                            HandleSpawnPlayer(p, mObj);
                            HandleRequestOtherPlayers(p, mObj);
                            break;

                        case "GM":
                            HandleGlobalMessage(p, mObj);
                            break;

                        case "RtP":
                            break;

                        case MsgLabels.Ident.LeaveWorld:
                            HandleLeaveWorld(p, mObj);
                            break;

                        case "rAI": // request AI (bots, etc.)??
                            HandleRequestAI(p, mObj);
                            break;

                        case "rAIp": // ??
                            HandleRequestAIp(p, mObj);
                            break;

                        case "Rez":
                            if (p == null)
                                break;

                            if (p.world == null)
                                break;

                            mObj["U"] = p.Data.UserID;
                            p.world.Broadcast(ref mObj, p);
                            break;

                        case MsgLabels.Ident.WearableUsed:
                            HandleWearableUsed(p, mObj);
                            break;
                        case MsgLabels.Ident.WearableRemoved:
                            HandleWearableRemoved(p, mObj);
                            break;

                        case "C":
                            HandleCollect(p, mObj["CollectableID"]);
                            break;

                        case "uP":
                            // Potion usage
                            break;

                        case "RsP":
                            HandleRespawn(p, mObj);
                            break;

                        case "GAW":
                            HandleGetActiveWorlds(p);
                            break;

                        case "TDmg":
                            {
                                if (p != null)
                                {
                                    if (p.world != null)
                                    {
                                        BSONObject bsonobject = new BSONObject();
                                        bsonobject["ID"] = "TDmg";
                                        bsonobject["DBl"] = 0;
                                        bsonobject["Mp1X"] = p.Data.PosX;
                                        bsonobject["Mp1Y"] = p.Data.PosY;
                                        bsonobject[MsgLabels.TimeStamp] = DateTime.Now.Ticks;
                                        p.world.Broadcast(ref bsonobject);
                                        p.Send(ref mObj);
                                    }
                                }
                            }
                            break;


                        case "FKPBl":
                            {
                                if (p != null)
                                {
                                    if (p.world != null)
                                    {
                                        BSONObject bsonobjectt = new BSONObject();
                                        bsonobjectt["ID"] = "FKPBl";
                                        bsonobjectt["DBl"] = 0;
                                        bsonobjectt["Mp1X"] = p.Data.PosX;
                                        bsonobjectt["Mp1Y"] = p.Data.PosY;
                                        bsonobjectt[MsgLabels.TimeStamp] = DateTime.Now.Ticks;
                                        p.world.Broadcast(ref bsonobjectt);
                                        p.Send(ref mObj);
                                        HandleRespawn(p, mObj);
                                    }
                                }
                                break;
                            }


                        case "XPCl":
                            {
                                BSONObject bsonobject = new BSONObject();
                                bsonobject["ID"] = "XPCl";
                                p.Data.lvl++;
                                p.Send(ref bsonobject);
                            }
                            break;
                        case "PDC":
                            {
                                if (p != null)
                                {
                                    if (p.world != null)
                                    {
                                        BSONObject rsp = new BSONObject();
                                        rsp["ID"] = "UD";
                                        rsp["U"] = p.Data.UserID;
                                        rsp["x"] = p.world.SpawnPointX;
                                        rsp["y"] = p.world.SpawnPointY;
                                        rsp["DBl"] = 0;
                                        p.world.Broadcast(ref rsp);
                                        p.Send(ref mObj);
                                    }
                                }
                                break;
                            }

                        case "Di":
                            HandleDropItem(p, mObj);
                            break;
                        case MsgLabels.Ident.RemoveInventoryItem:
                            HandleTrashItem(p, mObj);
                            break;
                        case "mp":
                            // Not sure^^
                            break;

                        case MsgLabels.Ident.MovePlayer:
                            HandleMovePlayer(p, mObj);
                            break;



                        case MsgLabels.Ident.SetBlock:
                            HandleSetBlock(p, mObj);
                            break;

                        case MsgLabels.Ident.SetBackgroundBlock:
                            HandleSetBackgroundBlock(p, mObj);
                            break;

                        case MsgLabels.Ident.HitBlock:
                            HandleHitBlock(p, mObj);
                            break;

                            
                        case MsgLabels.Ident.HitBackgroundBlock:
                            HandleHitBackground(p, mObj);
                            break;

                        case MsgLabels.Ident.HitBlockWater:
                            HandleHitBlockWater(p, mObj);
                            break;
                            

                        case MsgLabels.Ident.SetBlockWater:
                            HandleSetBlock(p, mObj);
                            break;

                        case MsgLabels.Ident.SyncTime:
                            HandleSyncTime(client);
                            break;
                        case MsgLabels.Ident.ChangeOrb:
                            HandleOrbChange(p, mObj);
                            break;
                        case MsgLabels.Ident.ChangeWeather:
                            HandleWeatherChange(p, mObj);
                            break;
                        case MsgLabels.Ident.Summon:
                            HandleSummon(p, mObj);
                            break;
                        case MsgLabels.Ident.KickPlayer:
                            HandleKick(p, mObj);
                            break;
                        case MsgLabels.Ident.BanPlayer:
                            HandleBan(p, mObj);
                            break;
                        case MsgLabels.Ident.WorldItemUpdate:
                            HandleWorldItemUpdate(p, mObj);
                            break;
                        case MsgLabels.Ident.GetRecentWorlds:
                            HandleRecentWorlds(p);
                            break;
                        case MsgLabels.Ident.TeleportAdmin:
                            HandleAdminTeleport(p, mObj);
                            break;
                        case MsgLabels.Ident.AdminKill:
                            //HandleAdminKill(p, mObj);
                            break;
                        case MsgLabels.Ident.AdminUnderCover:
                            HandleAdminUnderCover(p, mObj);
                            break;
                        case MsgLabels.Ident.AuctionHouseGetItems:
                            HandleAuctionHouseGetItems(p, mObj);
                            break;
                        case MsgLabels.Ident.AuctionBuyItem:
                            HandleAuctionHouseBuyItem(p, mObj);
                            break;
                        case MsgLabels.Ident.AuctionSellItem:
                            AHSellMsg(p, mObj);
                            break;
                  
                        case MsgLabels.Ident.AuctionGetitemSellHistory:
                            HandleAuctionHouseGetItemHistory(p,mObj);
                            break;
                        case MsgLabels.Ident.AuctionGetPlayerItemListing:
                            AHGetPItems(p, mObj);
                            break;
                        default:
                            pServer.OnPing(client, 1);
                            break;
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }



            }
        }

        private byte[] playerDataTemp = File.ReadAllBytes("player.dat").Skip(4).ToArray(); // template for playerdata, too painful to reverse rn so I am just gonna modify whats needed.
        public void HandlePlayerLogon(FeatherClient client, BSONObject bObj)
        {
#if DEBUG
            Util.Log("Handling player logon...");
#endif

            var resp = SimpleBSON.Load(playerDataTemp)["m0"] as BSONObject;
            var accHelper = pServer.GetAccountHelper();
            string clientIP = client.GetIPString();


            Player p = accHelper.LoginPlayer(client.GetIPString()); // Modify the parameters as needed
            if (p == null)
            {
                Util.Log("Player was null upon logon!!");
                client.DisconnectLater();
                return;
            }

            if (p.Client == null)
            {
                Util.Log("Client was null, so setting it here!");
                p.SetClient(client);
                client.data = p.Data;
            }

            string userID = p.Data.UserID;


            // Check if the client's IP address has changed
            if (p.Data.LastIP != clientIP)
            {
                // Update the player's IP address in the database
                p.Data.LastIP = clientIP;
               
                p.Save(); // Assuming you have an Update method in your Player class
            }

            if (!pServer.players.ContainsKey(userID))
            {
                pServer.players[userID] = p; 
            }
            else
            {
                p = pServer.players[userID];

                if (p.isInGame)
                {
                    Util.Log("Account is online already, disconnecting current client!");
                    if (p.Client != null)
                    {
                        if (p.Client.isConnected())
                        {
                            p.Client.Send(new BSONObject("DR"));
                            p.Client.DisconnectLater();
                        }
                    }
                }
            }

            BSONObject pd = new BSONObject("pD");
            pd[MsgLabels.PlayerData.ByteCoinAmount] = p.Data.Coins;
            pd[MsgLabels.PlayerData.GemsAmount] = p.Data.Gems;
            pd[MsgLabels.PlayerData.Username] = p.Data.Name.ToUpper();
            pd[MsgLabels.PlayerData.PlayerOPStatus] = (int)p.Data.adminStatus;
            pd[MsgLabels.PlayerData.InventorySlots] = 400;
            pd[MsgLabels.PlayerData.ShowOnlineStatus] = true;
            pd[MsgLabels.PlayerData.ShowLocation] = true;
            pd[MsgLabels.PlayerData.VIPClaimTime] = 638824024310000000;
            pd[MsgLabels.PlayerData.VIPEndTime] = 638824024310000000;
            pd[MsgLabels.PlayerData.NextDailyBonusGiveAway] = 638824024310000000;
            pd[MsgLabels.PlayerData.tutorialState] = "3";

            if (p.Data.Inventory.Count == 0)
            {
                p.inventoryManager.RegularDefaultInventory();
            }

            pd["ll"] = p.Data.lvl;
            pd["xpAmount"] = p.Data.XP;
            if (p.IsUnregistered() == false)
            {
                pd["nameChangeCounter"] = 1;
            }

            pd["experienceAmount"] = 1;
            pd["inv"] = p.inventoryManager.GetInventoryAsBinary();
            pd["tutorialState"] = 3;
            resp["rUN"] = p.Data.Name;
            resp["pD"] = SimpleBSON.Dump(pd);
            resp["U"] = p.Data.UserID;
            resp["Wo"] = "PIXELSTATION";
            resp["EmailVerified"] = true;
            resp["Email"] = p.IsUnregistered() ? "www.ltps.pw" : "www.ltps.pw";

            p.SetClient(client); // override client...
            client.data = p.Data;
            p.isInGame = true;

            client.Send(resp);
        }



        public void PVi(Player p, BSONObject bObj)
        {
            int IK = bObj["IK"];
            BSONObject packet = new BSONObject();
            packet["ID"] = "PVi";
            packet["x"] = 36;
            packet["y"] = 19;
            packet["vC"] = 4;
            packet["vI"] = 1;
            packet["IK"] = IK;
            packet["S"] = true;
            packet["Amt"] = 1;
            p.Send(ref packet);
        }

        public void AHSellMsg(Player p, BSONObject bObj)
        {
            if (p == null || p.world == null) return;

            int IK = bObj["IK"].int32Value;
            int Amt = bObj["Amt"];
            int BC = bObj["BCAmt"];
            long TM = bObj["TM"];
            int itemID = IK & 16777215;
            InventoryItemType inventoryItemType = (InventoryItemType)(IK >> 24);

            // Create a BSONObject for the packet
            BSONObject packet = new BSONObject();
            packet["ID"] = "AHSellMsg";
            packet["x"] = 36;
            packet["y"] = 19;
            packet["IK"] = IK;
            packet["S"] = true;
            packet["Amt"] = Amt;
            packet["BCAmt"] = BC;
            packet["TM"] = TM;

            string sellerid = p.Data.UserID;
            PWEHelper pweHelper = new PWEHelper(pServer);

            try
            {
                // Attempt to remove the item from the player's inventory
                p.inventoryManager.RemoveItemsFromInventory((WorldInterface.BlockType)itemID, inventoryItemType, (short)Amt);

                // Check if the item was successfully removed
                if (p.inventoryManager.HasItemAmountInInventory((WorldInterface.BlockType)itemID, inventoryItemType, (short)Amt))
                {
                    // Item was not removed, handle this case
                    BSONObject errorObj = new BSONObject("OMsg");
                    errorObj["ID"] = "OMsg";
                    errorObj["msg"] = "Error: Item could not be removed from inventory.";
                    p.Send(ref errorObj);
                    return;
                }

                // Add the item to the PWE
              //  pweHelper.CreatePWEShopResult(BC, itemID, inventoryItemType, sellerid, DateTime.UtcNow.Ticks, DateTime.UtcNow.AddDays(2).Ticks, Amt);

                // Send confirmation packet to the player
                p.Send(ref packet);

                // Log the action
                Console.WriteLine($"Player {p.Data.Name} has listed {Amt} of {itemID}:{inventoryItemType} for sale at {BC} bytecoins.");
            }
            catch (Exception ex)
            {
                // Handle the case where the item could not be removed
                BSONObject errorObj = new BSONObject("OMsg");
                errorObj["ID"] = "OMsg";
                errorObj["msg"] = "Error: Item could not be removed from inventory.";
                p.Send(ref errorObj);

                // Log the exception
                Console.WriteLine($"Error removing item from inventory for player {p.Data.Name}: {ex.Message}");
            }
        }


        public void CompleteSale(string buyerId, string sellerId, int itemID, InventoryItemType itemType, int amount, int price)
        {
            // Fetch the seller player object
            Player seller = pServer.GetPlayerByUserID(sellerId);
  

            if (seller != null)
            {
                // Credit the bytecoins to the seller
                seller.AddCoins(price);

                // Notify the seller about the sale
                BSONObject saleCompletedMsg = new BSONObject("OMsg");
                saleCompletedMsg["ID"] = "OMsg";
                saleCompletedMsg["msg"] = $"Your item {itemID}:{itemType} has been sold for {price} bytecoins.";
                seller.Send(ref saleCompletedMsg);

                // Log the sale
                Console.WriteLine($"Player {seller.Data.Name} has sold {amount} of {itemID}:{itemType} for {price} bytecoins.");
            }
        }

        public string HandleCommandClearInventory(Player p)

        {
            p.inventoryManager.ClearInventory();
            BSONObject r = new BSONObject("DR");
            p.Send(ref r);

            return "Cleared inventory!";
        }

        public void WOFTW(Player p, BSONObject bObj)
        {
            BSONObject response = new BSONObject();
            response[MsgLabels.MessageID] = "GWotW";
            response["ID"] = "GWotW";
            response["Count"] = 2;

            BSONObject worldInfo = new BSONObject();
            worldInfo["WorldID"] = "142";
            worldInfo["WorldName"] = "PIXELSTATION";
            worldInfo["WorldOwnerID"] = "1";
            worldInfo["WorldOwnerName"] = "efee";
            worldInfo["WorldTag"] = 15;
            worldInfo["RatingAverage"] = 4.2413430213928223;
            worldInfo["RatingCount"] = 1000;
            worldInfo["WorldOfTheWeekDate"] = DateTime.Now.AddTicks(638370661810000000);

            BSONObject worldInfo2 = new BSONObject();
            worldInfo2["WorldID"] = "142";
            worldInfo2["WorldName"] = "LTPS";
            worldInfo2["WorldOwnerID"] = "1";
            worldInfo2["WorldOwnerName"] = "efee";
            worldInfo2["WorldTag"] = 15;
            worldInfo2["RatingAverage"] = 4.2413430213928223;
            worldInfo2["RatingCount"] = 1000;
            worldInfo2["WorldOfTheWeekDate"] = DateTime.Now.AddTicks(638370661810000000);

            response["W1"] = worldInfo;
            response["W0"] = worldInfo2;

            p.Send(ref response);
        }



        public string HandleCommandGlobalMessage(Player p, string[] args)
        {
            if (args.Length < 2)
            {
                return "Usage: /gm (your message)";
            }

            string msg_query = "";

            for (int i = 1; i < args.Length; i++)
            {
                msg_query += args[i];

                if (i < args.Length - 1) msg_query += " ";
            }


            if (p.Data.Gems >= 500)
            {
                p.RemoveGems(500);
                BSONObject gObj = new BSONObject(MsgLabels.Ident.BroadcastGlobalMessage);
                gObj[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage($"<color=#C576F6>{p.Data.Name}", p.world.WorldName, p.world.WorldName, 1,
                   msg_query);

                pServer.Broadcast(ref gObj);

                return "";
            }
            else
            {
                return "Not enough gems to send a Global Message! (You need 500 Gems to sent broadcast).";
            }
        }

        public string HandleCommandPay(Player p, string[] args)
        {
            if (args.Length < 3)
                return "Usage: /pay (name) (gems amount)";

            string user = args[1];
            int amt;
            int.TryParse(args[2], out amt);

            if (amt < 100 || amt > 999999)
            {
                return "Can only send gems between 100 and 999999!";
            }

            if (p.Data.Gems < amt)
                return "Not enough gems to transfer.";

            var player = pServer.GetOnlinePlayerByName(user);
            if (player == null)
                return String.Format("{0} is offline.", user);

            if (player == p)
                return "Cannot transfer gems to yourself, nice try!";

            p.RemoveGems(amt);
            player.AddGems(amt);

            return String.Format("Transfered {0} Gems to Account {1}!", amt, player.Data.Name);


        }


        public string HandleCommandPayBc(Player p, string[] args)
        {
            if (args.Length < 3)
                return "Usage: /paybc (name) (bytecoin amount)";

            string user = args[1];
            int amt;
            int.TryParse(args[2], out amt);

            if (amt < 50 || amt > 999999)
            {
                return "Can only send bytecoins between 50 and 999999!";
            }

            if (p.Data.Coins < amt)
                return "Not enough bytecoins to transfer.";

            var player = pServer.GetOnlinePlayerByName(user);
            if (player == null)
                return String.Format("{0} is offline.", user);

            if (player == p)
                return "Cannot transfer gems to yourself, nice try!";

            p.RemoveCoins(amt);
            player.AddCoins(amt);

            return String.Format("Transfered {0} Bytecoins to Account {1}!", amt, player.Data.Name);


        }



        public string HandleCommandPromo(Player p, string[] args)
        {
            if (args.Length < 2)
                return "Usage: /promo (promo code)";

            string promoCode = args[1];

            // Check if the promo code is valid
            bool isValidPromo = ValidatePromoCode(promoCode);

            if (!isValidPromo)
            {
                return "Invalid promo code. Get a new promo code on https://ltps.pw/discord";
            }

            if (p.Data.Promo == 3)
            {
                return "You already claimed this promo code or expired code!";
            }

            // Process the valid promo code
            ProcessPromoCode(p, promoCode);
            p.Data.Promo = 3;
            return "Promo code applied successfully! 200024 Gems given.";
        }

        // Implement your promo code validation logic
        private bool ValidatePromoCode(string promoCode)
        {
            // Add your validation logic here
            // For example, check against a list of valid promo codes or perform any other necessary checks
            // Return true if the promo code is valid, otherwise return false
            // Replace the following line with your validation logic

            // Example: Check against a list of valid promo codes
            List<string> validPromoCodes = new List<string> { "NO VALID 032930323" };
            return validPromoCodes.Contains(promoCode.ToUpper());
        }

        // Implement your promo code processing logic
        private void ProcessPromoCode(Player p, string promoCode)
        {
            // Add your logic to handle the promo code
            // This can include updating player data, granting bonuses, etc.
            // Replace the following line with your processing logic
            Console.WriteLine($"Processed promo code '{promoCode}' for player {p.Data.Name}");
        }
        public string HandleCommandWarn(Player p, string[] args)
        {
            if (args.Length < 3)
                return "Usage: /warn (name) (reason)";

            string user = args[1];
            string reason = args[2];


            var player = pServer.GetOnlinePlayerByName(user);
            if (player == null)
                return String.Format("{0} is offline. You cant warn offline player.", user);


            BSONObject gObja = new BSONObject(MsgLabels.Ident.BroadcastGlobalMessage);
            gObja[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage($"<color=#FF0000>System", p.world.WorldName, p.world.WorldName, 1,
               String.Format("{1} is warned by admin! If you keep breaking rules, you might be get banned! ", reason, player.Data.Name));


            p.world.Broadcast(ref gObja);

            return String.Format("Warned Player: {1}!", reason, player.Data.Name);


        }


       

        public string HandleCommandConvert(Player p, string[] args)
        {
            if (args.Length < 2)
                return "Usage: /convert (amount)";

            if (!int.TryParse(args[1], out int amount))
                return "Invalid amount. Please provide a valid number.";

            // Convert the amount
            p.RemoveCoins(5);

            // Send message to Discord webhook
         

            return $"Converted {amount} coins. New balance: {p.Data.Coins} coins.";
        }


        public void HandleQueryPlayerInfo(Player p, BSONObject bObj)
        {
            if (p == null) return;
            if (p.world == null) return;
            var player = pServer.GetOnlinePlayerByName(p.Data.ToString());
            if (player == null) return;
            var data = new List<int>();
            BSONObject wObj = new BSONObject();

        
            wObj["ID"] = "QPi";
            wObj["TuID"] = bObj.stringValue;
            //wObj["QueryResult"] = [14, 1, "VIP", 0, 149, 999, , , , , , ];
        }

       

        public string HandleUnban(Player p, string[] args)
        {

            if (args.Length < 2)
                return "Usage: /unban (Username)";

            string username = args[1];

            string filePath = "banned.txt";

            string stringToRemove = username;
            string fileContent = File.ReadAllText(filePath);
            string modifiedContent = fileContent.Replace(stringToRemove, string.Empty);
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.Write(modifiedContent);
            }

            return String.Format("Unbanned Player!");


        }




        public string HandleCommandRegister(Player p, string[] args)
        {
            if (args.Length < 3)
                return "Usage: /register (name) (password)";

            string name = args[1], pass = args[2];

            if (SQLiteManager.HasIllegalChar(name) || SQLiteManager.HasIllegalChar(pass))
                return "Username or password has illegal character! Only letters and numbers.";

            if (pass.Length > 24 || name.Length > 24 || pass.Length < 3 || name.Length < 3)
                return "Username or Password too long or too short!";

            if (!p.IsUnregistered())
                return "You are registered already!";

            var sql = pServer.GetSQL();

           using (var read = sql.FetchQuery($"SELECT COUNT(*) FROM players WHERE IP='{p.Data.LastIP}'"))
            {
                if (read.Read())
                {
                    int ipCount = read.GetInt32(0);
                    if (ipCount >= 2)
                        return "You are not allowed to register more than twice from the same IP address!";
                }
            }

            // Check if an account with the same name already exists
            using (var read = sql.FetchQuery($"SELECT * FROM players WHERE Name='{name}'"))
            {
                if (read.HasRows)
                    return "An account with this name already exists!";
            }

            if (sql.Query($"UPDATE players SET Name='{name}', Pass='{pass}', IP='{p.Data.LastIP}' WHERE ID='{p.Data.UserID}'") > 0)
            {
                p.Data.Name = name;
                BSONObject r = new BSONObject("DR");

                p.Send(ref r);
                return "";
            }

            return "Couldn't register right now, try again!";
        }




        public string HandleCommandLogin(Player p, string[] args)
        {
            if (args.Length < 3)
                return "Usage: /login (name) (password)";

            string name = args[1], pass = args[2];

            if (SQLiteManager.HasIllegalChar(name) || SQLiteManager.HasIllegalChar(pass))
                return "Username or password has illegal character! Only letters and numbers.";

            if (pass.Length > 24 || name.Length > 24 || pass.Length < 3 || name.Length < 3)
                return "Username or Password too long or too short!";

            if (!p.IsUnregistered())
                return "You are logged on already!";

            var sql = pServer.GetSQL();
            using (var read = sql.FetchQuery($"SELECT * FROM players WHERE Name='{name}' AND Pass='{pass}'"))
            {
                string uID = "0";

                if (!read.HasRows)
                    return "Account does not exist or password is wrong! Try again?";

                if (!read.Read())
                    return "Account does not exist or password is wrong! Try again?";

                uID = (string)read["ID"];


                var cmd = sql.Make("UPDATE players SET ID=@ID");
                cmd.Parameters.AddWithValue("@ID", uID);

                if (sql.PreparedQuery(cmd) > 0)
                {
                    BSONObject r = new BSONObject("DR");
                    p.Client.Send(r);
                    p.Client.Flush();

                    pServer.players.Remove(p.Data.UserID);
                    return "";
                }
            }

            return "Couldn't login right now, try again!";
        }



        public string HandleCommandChangeDiscord(Player p, string[] args)
        {
            if (args.Length < 2)
                return "Usage: /discord (new username)";

            string newUsername = args[1];

            if (SQLiteManager.HasIllegalChar(newUsername))
                return "Username has an illegal character! Only letters and numbers are allowed.";

            if (newUsername.Length > 24 || newUsername.Length < 3)
                return "Username is too long or too short!";

            if (p.IsUnregistered())
                return "You need to be registered to change your username.";

            var sql = pServer.GetSQL();

            if (sql.Query($"UPDATE players SET Salt='{newUsername}' WHERE ID='{p.Data.UserID}'") > 0)
            {
                p.SelfChat($"Your Discord username changed to: {newUsername}");
                BSONObject warn = new BSONObject();
                warn["ID"] = "OMsg";
                warn["msg"] = $"4;LTPS;Your Discord username changed to: {newUsername}";
                p.Send(ref warn);

                return "";
            }

            return "Couldn't change the username right now, try again!";
        }


        public string HandleCommandChangePass(Player p, string[] args)
        {
            if (args.Length < 2)
                return "Usage: /changepass (new password)";

            string newPass = args[1];

            if (SQLiteManager.HasIllegalChar(newPass))
                return "Password has an illegal character! Only letters and numbers are allowed.";

            if (newPass.Length > 24 || newPass.Length < 3)
                return "Password is too long or too short!";

            if (p.IsUnregistered())
                return "You need to be registered to change your password.";

            var sql = pServer.GetSQL();

            if (sql.Query($"UPDATE players SET Pass='{newPass}' WHERE ID='{p.Data.UserID}'") > 0)
            {
                p.SelfChat($"Your password changed to: {newPass}");
                BSONObject warn = new BSONObject();
                warn["ID"] = "OMsg";
                warn["msg"] = $"4;LTPS;Your password changed to: {newPass}";
                p.world.Broadcast(ref warn);

                return "";
            }

            return "Couldn't change the password right now, try again!";
        }

        public void HandleWorldChatMessage(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            string msg = bObj["msg"];

            string[] tokens = msg.Split(" ");
            int tokCount = tokens.Count();

            if (tokCount <= 0)
                return;

            if (tokens[0] == "")
                return;

            List<string> illegalWords = new List<string>
    {
        "pwps",
        "http",
        "https",
        "discord.gg",
        ".gg",
        "com",
        "org",
        "www"
    };


            string lowerMsg = msg.ToLower();


            if (illegalWords.Any(illegalWord => lowerMsg.Contains(illegalWord)))
            {
                return;
            }


            if (tokens[0][0] == '/')
            {
                string res = "Unknown command.";
                switch (tokens[0])
                {
                    case "/hElp":
                    case "/helP":
                    case "/help":
                    case "/Help":
                    case "/HELP":
                    case "/HELp":
                        res = "Commands >> /help , /give (item id) , /find (item name) , /login (username pass), /mypass , /changepass , /discord , /autologin , /pay (username amount) , /paybc , /gm , /clearworld , /online , /promo";
                        break;

                    case "/GM":
                    case "/gm":
                        if (string.Equals(tokens[0], "/gm", StringComparison.Ordinal))
                        {
                            res = HandleCommandGlobalMessage(p, tokens);
                        }
                        break;




                    case "/Pay":
                    case "/pay":
                    case "/PAY":
                        {
                            res = HandleCommandPay(p, tokens);
                            break;
                        }

                    case "/Paybc":
                    case "/paybc":
                    case "/PAYBC":
                        {
                            res = HandleCommandPayBc(p, tokens);
                            break;
                        }

                    case "/Find":
                    case "/FIND":
                    case "/find":
                        {
                            if (tokCount < 2)
                            {
                                res = "Usage: /find (item name)";
                                break;
                            }

                            string item_query = "";

                            for (int i = 1; i < tokens.Length; i++)
                            {
                                item_query += tokens[i];

                                if (i < tokens.Length - 1) item_query += " ";
                            }

                            if (item_query.Length < 2)
                            {
                                res = "Please enter an item name with more than 2 characters!";
                                break;
                            }

           
                            item_query = item_query.ToLower();

                            var items = ItemDB.FindByAnyName(item_query);

                            if (items.Length > 0)
                            {
                                string found = "";

                                foreach (var it in items)
                                {
                                    found += $"\n{it.name} ID: {it.ID}";
                                }

                                res = $"Found items:{found}";
                            }
                            else
                            {
                                res = "No items found.";
                            }
                            break;
                        }

                    case "/CLEARWORLD":
                    case "/clearworld":
                        var wori = p.world;
                        if (p.world.lockWorldData != null && ((!p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID))))
                        {

                            string user = p.Data.UserID;
                            var player = pServer.GetOnlinePlayerByName(user);
                            BSONObject warns = new BSONObject();
                            warns["ID"] = "OMsg";
                            warns["msg"] = $"4;LTPS System;You are not owner of the world!";
  
                            p.Send(ref warns);

                            return;
                        }
                        else
                        {


                            if (p.Data.Gems >= 1500)
                            {
                                wori.SetupTerrainClear();
                                res = "World has been cleared successfully!";
                                BSONObject kickData = new BSONObject("DR");
                                p.world.Broadcast(ref kickData);
                                p.RemoveGems(1500);
                            }
                            else
                            {
                                BSONObject warnz = new BSONObject();
                                warnz["ID"] = "OMsg";
                                warnz["msg"] = $"4;LTPS System;Not enough gems to clear world! You need 1500 gems to clear the world.";
                                p.world.Broadcast(ref warnz);
                               
                            }

                      

                      
                            break;
                        }





                    case "/MYPASS":
                    case "/mypass":
                        res = HandleCommandMyPassword(p, tokens);
                        break;

             



                    case "/REGISTER":
                    case "/register":
                        res = HandleCommandRegister(p, tokens);
                        break;


                    case "/shop":
                        BSONObject mObj = new BSONObject();
                        mObj["ID"] = "OMsg";
                        mObj["msg"] = $"4;LTPS;Shop is closed. You can purchase items from PWE Terminal now.";
                        p.world.Broadcast(ref mObj);
                        break;

                  

                    case "/admin":
                        if (p.Data.adminStatus != AdminStatus.AdminStatus_Admin) return;
                        {
                            res = "Admin Commands | Dont abuse, ok?\n1- /warn (player name) (reason)\n2- /unban username (Note: case sensitive)\n3- /mute (username)";
                        }
                        break;

                    case "/warn":
                        if (p.Data.adminStatus != AdminStatus.AdminStatus_Admin) return;
                        {
                            res = HandleCommandWarn(p, tokens);
                        }
                        break;

                    case "/unban":
                        if (p.Data.adminStatus != AdminStatus.AdminStatus_Admin) return;
                        {
                            res = HandleUnban(p, tokens);
                        }
                        break;





                     








                    case "/INFLUENCER":
                    case "/influencer":
                        if (p.Data.Gems >= 200000)
                        {
                            res = "Bought Influencer role for 200000 Gems!";
                            p.RemoveGems(200000);
                            p.pSettings.Set(PlayerSettings.Bit.SET_INFLUENCER);
                            BSONObject awsaa = new BSONObject("DR");
                            p.Send(ref awsaa);

                        }
                        else
                        {
                            res = "Influencer Role is 200.000 Gems. Not enough gems to purchase!\nInfluencer Role Pack includes: @In-Game Influencer Role";
            
                        }
                        
                        break;



                    case "/AUTOLOGIN":
                    case "/Autologin":
                    case "/autologin":
                        res = HandleCommandAutoLogin(p, tokens);

                        break;

                    case "/LOGIN":
                    case "/login":
                    case "/Login":
                        res = HandleCommandLogin(p, tokens);

                        break;

                    case "/CHANGEPASS":
                    case "/changepass":
                    case "/Changepass":
                        if (string.Equals(tokens[0], "/changepass", StringComparison.Ordinal))
                        {
                            res = HandleCommandChangePass(p, tokens);
                        }
                        break;

                    case "/DISCORD":
                    case "/discord":
                    case "/Discord":
                        res = HandleCommandChangeDiscord(p, tokens);
                        break;

                    case "/promo":
                        res = HandleCommandPromo(p, tokens);
                        break;

                    case "/ONLINE":
                    case "/online":
                        res = ($"{pServer.GetPlayersIngameCount()} players are online.");
                        break;

                    case "/GivE":
                    case "/GIVE":
                    case "/Give":
                    case "/give":
                        if (p.Data.Coins >= 0)
                        {

                            if (tokCount < 2)
                            {
                                res = "Usage: /give (Item ID)";
                            }
                            else
                            {
                                int id;
                                int.TryParse(tokens[1], out id);
                                
                                var it = ItemDB.GetByID(id);

                                if (it.ID <= 0)
                                {
                                    res = $"Item {id} not found!";
                                }
                                else
                                {
                                    if (Shop.ContainsItem(id))
                                    {
                                        res = "This item is unobtainable or purchaseable via bytecoins on PWE Terminal.";
                                        break;
                                    }
                                   
                                    if (it.name.EndsWith("Block"))
                                    {

                                        p.world.Drop(id, 100, p.Data.PosX, p.Data.PosY, ItemDB.GetByID(id).type);
                                        res = @$"Given 100 {it.name}.";
                                        HandleCollect(p, ItemDB.GetByID(id).type);
                                        break;
                                    }

                                    if (it.name.EndsWith("Wallpaper"))
                                    {
                                   
                                        p.world.Drop(id, 100, p.Data.PosX, p.Data.PosY, ItemDB.GetByID(id).type);
                                        res = @$"Given 100 {it.name}.";
                                        HandleCollect(p, ItemDB.GetByID(id).type);
                                        break;
                                    }

                                    if (it.name.EndsWith("Background"))
                                    {

                                        p.world.Drop(id, 100, p.Data.PosX, p.Data.PosY, ItemDB.GetByID(id).type);
                                        res = @$"Given 100 {it.name}.";
                                        HandleCollect(p, ItemDB.GetByID(id).type);
                                        break;
  
                                    }


                                    p.world.Drop(id, 1, p.Data.PosX, p.Data.PosY, ItemDB.GetByID(id).type);
                                   

                                    res = @$"Given 1 {it.name}.";

                                    HandleCollect(p, ItemDB.GetByID(id).type);

                                }
                            }


                        }
                        break;

                }
                
                if (res != "")
                {
                    bObj[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage("<color=#F19F9F>LTPS",
                        p.world.WorldName,
                        p.world.WorldName,
                        1,
                        res);

                    p.Send(ref bObj);
                }
            }
            else
            {
                bObj[MsgLabels.MessageID] = "WCM";
                bObj[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage(p.Data.Name, p.Data.UserID, "#" + p.world.WorldName, 0, msg);
                p.world.Broadcast(ref bObj, p);
            }
        }

  

        public void HandleGive(Player p, BSONObject bObj)
        {
           
        }

        public void HandleMoreWorldInfo(Player p, BSONObject bObj)
        {
            if (p == null)
                return;
            string worldName = bObj["WN"];

            var w = pServer.GetWorldManager().GetByName(bObj["WN"]);


            List<string> manuallyAddedWorldNames = new List<string>
            {
                "PIXELSTATION", "PIXELMINES", "HALLOWEENCASTLE", "LTPS"
            };

            if (manuallyAddedWorldNames.Contains(worldName))
            {
                bObj["Ct"] = -1;  // Manual world
                p.Send(ref bObj);
            }
            else
            {
                if (WorldSession.WorldExists(worldName))
                {

                    bObj[MsgLabels.Count] = w == null ? 0 : w.Players.Count;
                    p.Send(ref bObj);
                }
                else
                {
                    bObj["Ct"] = -3;
                    p.Send(ref bObj);

                }
            }
        }



        public void HandlePlayerStatusChange(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;


            bObj["U"] = p.Data.UserID;
            p.world.Broadcast(ref bObj, p);
        }

        public string HandleCommandMyPassword(Player p, string[] args)
        {
            if (args.Length < 1)
                return "Usage: /mypass";

            var sql = pServer.GetSQL();


            using (var read = sql.FetchQuery($"SELECT Pass FROM players WHERE ID='{p.Data.UserID}'"))
            {
                if (read.Read())
                {
                    string storedPassword = read.GetString(0);

                    BSONObject kickData = new BSONObject("OMsg");
                    kickData["ID"] = "OMsg";
                    kickData["msg"] = $"4;LTPS;Your password: {storedPassword}";
                    p.Send(ref kickData);
                    return "";
                }

            }

            return "An error occurred while fetching your password.";
        }

        public void HandleShopPurchase(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            string id = bObj["IPId"];
            Util.Log(id);
            bObj["S"] = "PS";

            if (Shop.offers.ContainsKey(id))
            {
                var s = Shop.offers[id];


                if (s.items != null)
                {
                    if (p.Data.Gems >= s.price)
                    {
                        bObj["IPRs"] = s.items.SelectMany(item => Enumerable.Repeat(item.Key, item.Value2)).ToList();

                        foreach (var item in s.items)
                        {
                            p.inventoryManager.AddItemToInventory((BlockType)item.Key, item.Value1, (short)item.Value2);
                        }

                        p.RemoveGems(s.price);
                    }
                    else
                    {
                        return;
                    }
                }
                //else if()
            }
            else if(Shop.byteOffers.ContainsKey(id))
            {
                var s = Shop.byteOffers[id];


                if (p.Data.Gems >= s.price)
                {
                    p.RemoveGems(s.price);
                    p.Data.Coins += s.amount;
                }
                else
                {
                    return;
                }
            }
            else { return; }



            bObj["IPRs2"] = new List<int>();

            p.Send(ref bObj);
        }


  

        private string GenerateRandomToken(int length)
        {
            const string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var token = new string(Enumerable.Repeat(allowedChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return token;
        }

        public void HandleRenamePlayer(Player p, BSONObject bObj)
        {
            string name = bObj["UN"];
            string pass = GenerateRandomToken(24);
            var sql = pServer.GetSQL();

   
            using (var read = sql.FetchQuery($"SELECT * FROM players WHERE Name='{name}'"))
            {
                if (read.HasRows)
                {
                    BSONObject loggeds = new BSONObject("OMsg");
                    loggeds["ID"] = "OMsg";
                    loggeds["msg"] = $@"4;System Warning;<color=#FF0000><b>Username is taken, please select different one.";
                    p.Send(ref loggeds);
                    return;
                }
            }





            if (sql.Query($"UPDATE players SET Name='{name}', Pass='{pass}' WHERE ID='{p.Data.UserID}'") > 0)

            {
                p.Data.Name = name;
                _ = bObj["S"] = true;
                _ = bObj["RenameValue"] = false;
                p.Send(ref bObj);

                BSONObject logged = new BSONObject("OMsg");
                logged["ID"] = "OMsg";
                logged["msg"] = $@"4;System Warning;<color=#FF0000><b>System generated a random password for your account, please change your password with /changepass (new password) command. Or you can use /mypass command to see ur current password. Dont forget to note it!";
                p.Send(ref logged);
                p.Save();
                return;
            }
            using (var read = sql.FetchQuery($"SELECT * FROM players WHERE Name='{name}'"))
            {
                if (read.HasRows)
                {
                    _ = bObj["S"] = false;
                    _ = bObj["ER"] = 7;
                    p.Send(ref bObj);
                }
                return;
            }

        }



        public void HandleTryToJoinWorld(Player p, BSONObject bObj, string wldName = "")
        {
            if (p == null)
            {
                Util.Log("p is null");
                return;
            }

            Util.Log($"Player with userID: {p.Data.UserID.ToString()} is trying to join a world [{pServer.GetPlayersIngameCount()} players online!]...");

            BSONObject resp = new BSONObject(MsgLabels.Ident.TryToJoinWorld);
            resp[MsgLabels.JoinResult] = (int)MsgLabels.WorldJoinResult.TooManyPlayersInWorld;
            if (bObj.ContainsKey("W"))
            {
                resp["WN"] = bObj["W"];
                resp["WB"] = 0;
            }

            var wmgr = pServer.GetWorldManager();
            string worldName = bObj["W"];

            

            // Continue with world join logic
            WorldSession world = wmgr.GetByName(worldName, true);

            if (SQLiteManager.HasIllegalChar(worldName))
            {
                resp[MsgLabels.JoinResult] = (int)MsgLabels.WorldJoinResult.NotValidWorldName;
            }
            else if (world.IsPlayerBanned(p))
            {
                resp[MsgLabels.JoinResult] = (int)MsgLabels.WorldJoinResult.UserIsBanned;
                resp["BanState"] = "World";
                resp["T"] = world.banList[p.Data.UserID];
                resp["BPUR"] = "Breaking the Game Rules";
                resp["BPl"] = 1;

            }
            else if (world != null)
            {
#if DEBUG
                Util.Log("World not null, JoinResult SUCCESS, joining world...");
#endif
                resp[MsgLabels.JoinResult] = (int)MsgLabels.WorldJoinResult.Ok;
            }
            else
            {
                resp[MsgLabels.JoinResult] = (int)MsgLabels.WorldJoinResult.TooManyPlayersInWorld;
            }

            p.Send(ref resp);
        }



        public void HandleGetWorld(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            HandleLeaveWorld(p, null);

            string worldName = bObj["W"];
            var wmgr = pServer.GetWorldManager();

            WorldSession world = wmgr.GetByName(worldName, true);
            if (SQLiteManager.HasIllegalChar(worldName))
            {
                return;
            }
            else if (world.IsPlayerBanned(p))
            {
                return;
            }
            else if (world == null)
            {
                return;
            }
            p.AddRecentWorld(world.WorldName, world.WorldID);
            p.SaveRecentWorlds();
            world.AddPlayer(p);

            BSONObject resp = new BSONObject();
            BSONObject wObj = world.Serialize();

            resp[MsgLabels.MessageID] = MsgLabels.Ident.GetWorldCompressed;
            resp["W"] = Util.LZMAHelper.CompressLZMA(SimpleBSON.Dump(wObj));

            p.Send(ref resp);
            p.Tick();

            p.isLoadingWorld = true;
        }

        public void HandleLeaveWorld(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            BSONObject resp = new BSONObject("PL");
            resp[MsgLabels.UserID] = p.Data.UserID;

            p.world.Broadcast(ref resp, p);

            if (bObj != null)
                p.Send(ref bObj);

            p.world.RemovePlayer(p);
            p.isLoadingWorld = false;

            Util.Log($"Player with UserID {p.Data.UserID} left the world!");
        }

        public void HandleRequestOtherPlayers(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            //p.Send(ref bObj);


            long kukTime = Util.GetKukouriTime();
            foreach (var player in p.world.Players)
            {
                if (player.Data.UserID == p.Data.UserID)
                    continue;

                string prefix = "";
                switch (player.pSettings.GetHighestRank())
                {

                    case Ranks.INFLUENCER:
                        prefix = "<color=#69de05>";
                        break;

                    case Ranks.ADMIN:
                        prefix = "<color=#E744DE>";
                        break;

                    case Ranks.MODERATOR:
                        prefix = "<color=#42e2fa>";
                        break;

                    default:
                        break;
                }

                BSONObject pObj = new BSONObject("AnP");
                pObj["x"] = player.Data.PosX;
                pObj["y"] = player.Data.PosY;
                pObj["t"] = kukTime;
                pObj["a"] = player.Data.Anim;
                pObj["d"] = player.Data.Dir;
                List<int> spotsList = new List<int>();
                //spotsList.AddRange(player.GetSpots());
     

                pObj["spots"] = spotsList;
                pObj["familiar"] = 0;
                pObj["familiarName"] = "LTPS";
                pObj["familiarLvl"] = 0;
                pObj["familiarAge"] = kukTime;
                pObj["isFamiliarMaxLevel"] = false;
                pObj["UN"] = prefix + player.Data.Name;
                pObj["U"] = player.Data.UserID;
                pObj["Age"] = 69;
                pObj["LvL"] = 10;
                pObj["xpLvL"] = 10;
                pObj["pAS"] = 0;
                pObj["PlayerAdminEditMode"] = false;
                pObj[MsgLabels.PlayerData.PlayerOPStatus] = (int)player.pSettings.GetHighestRank();
                pObj["Ctry"] = 999;
                pObj["GAmt"] = player.Data.Gems;
                pObj["ACo"] = 0;
                pObj["QCo"] = 0;
                pObj["Gnd"] = 0;
                pObj["skin"] = 7;
                pObj["faceAnim"] = 0;
                pObj["inPortal"] = false;
                pObj["SIc"] = 0;
                pObj["D"] = 0;
                pObj["VIPEndTimeAge"] = kukTime;
                pObj["IsVIP"] = true;

                p.Send(ref pObj);
            }

            p.Send(ref bObj);
        }

        public enum GlobalMessageResult
        {
            Unknown,
            Timeout,
            ConnectionFailed,
            AuthenticationFailed,
            NoMessage,
            NoSender,
            NoGems,
            Success,
        }



        public void HandleGlobalMessage(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            if (p.Data.Gems >= 5000)
            {
                p.RemoveGems(5000);

                var cmb = SimpleBSON.Load(Convert.FromBase64String(bObj["msg"]));

                string msg = cmb["message"].stringValue;
                if (msg.Length > 256)
                    return;

                BSONObject gObj = new BSONObject(MsgLabels.Ident.BroadcastGlobalMessage);
                gObj[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage($"<color=#DA83E88>{p.Data.Name}", p.world.WorldName, p.world.WorldName, 1,
                   msg);

                pServer.Broadcast(ref gObj);
            }
        }

    


       





      

        public void HandleSpawnPlayer(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            long kukTime = Util.GetKukouriTime();
            BSONObject pObj = new BSONObject();
            pObj[MsgLabels.MessageID] = "AnP";
            pObj["x"] = p.Data.PosX;
            pObj["y"] = p.Data.PosY;
            pObj["t"] = kukTime;
            pObj["a"] = p.Data.Anim;
            pObj["d"] = p.Data.Dir;
            List<int> spotsList = new List<int>();
            //  spotsList.AddRange(Enumerable.Repeat(35, 35));

            string prefix = "";
            switch (p.pSettings.GetHighestRank())
            {
                case Ranks.INFLUENCER:
                    prefix = "<color=#69de05>";
                    break;

                case Ranks.ADMIN:
                    prefix = "<color=#E744DE>";
                    break;

                case Ranks.MODERATOR:
                    prefix = "<color=#42e2fa>";
                    break;

                default:
                    break;
            }

            string filePath = "banned.txt";

            string searchText = p.Data.Name;

     
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                int lineNumber = 1;
                bool found = false;

                // Read and check each line from the file
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains(searchText))
                    {
                        Console.WriteLine($"This user is banned! {lineNumber}: {line}");
                        found = true;

                        BSONObject mObj = new BSONObject();
                        mObj["ID"] = "KPl";
                        mObj["BPl"] = 1;
                        mObj["WN"] = p.world.WorldName;
                        mObj["BanState"] = "Universal";
                        mObj["T"] = 638384523620000000;
                        mObj["BanFromGameReasonValue"] = "Breaking the Game Rules";

                        p.Send(ref mObj);
                    }
                    lineNumber++;
                }

                if (!found)
                {
                    Console.WriteLine($"This user  '{searchText}' is not banned.");
                }
            }



            //  int targetAmount = 2; 
            // int currentAmount = p.inventoryManager.HasItemAmountInInventory((WorldInterface.BlockType)3058, InventoryItemType.Familiar);

            //if (currentAmount > targetAmount)
            // {
            //   p.SelfChat("En az 2 petin var!"); // Buradaki mesaj istediğiniz gibi düzenlenebilir
            //  }


      


        

        pObj["spots"] = spotsList;
            pObj["familiar"] = 0;
            pObj["familiarName"] = "LTPS";
            pObj["familiarLvl"] = 0;
            pObj["familiarAge"] = kukTime;
            pObj["isFamiliarMaxLevel"] = true;
            pObj["UN"] = prefix + p.Data.Name;
            pObj["U"] = p.Data.UserID;
            pObj["Age"] = 69;
            pObj["LvL"] = 99;
            pObj["xpLvL"] = 99;
            pObj["pAS"] = 0;
            pObj["PlayerAdminEditMode"] = false;
            pObj["Ctry"] = 999;
            pObj["GAmt"] = p.Data.Gems;
            pObj["ACo"] = 0;
            pObj["QCo"] = 0;
            pObj["Gnd"] = 0;
            pObj["skin"] = 7;
            pObj["faceAnim"] = 0;
            pObj["starter"] = false;
            pObj["inPortal"] = false;
            pObj["SIc"] = 0;
            pObj["VIPEndTimeAge"] = kukTime;
            pObj[MsgLabels.PlayerData.PlayerOPStatus] = (int)p.pSettings.GetHighestRank();
            pObj["IsVIP"] = true;


            p.world.Broadcast(ref pObj, p);

            BSONObject cObj = new BSONObject("WCM");

            cObj[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage("<color=#F3F2F0>System",

                 p.world.WorldName,
                 p.world.WorldName,
                 1,
                 "\nCheckout all commands by using /help also you can purchase items with bytecoins at PWE Terminal visit PIXELSTATION to use.\nWebsite: https://ltps.pw/\n");
           
            p.Send(ref cObj);




        BSONObject cObjss = new BSONObject("WCM");

        cObjss[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage("<color=#FFA500>Double Gems Bonus Event",
      
         p.world.WorldName,
           p.world.WorldName,
            1,
              "\nBreaking Blocks and Backgrounds will give x2 Gems bonus for a limited time.");

        p.Send(ref cObjss);

            



            if (p.IsUnregistered())
            {


                BSONObject cObjs = new BSONObject("WCM");

                cObjs[MsgLabels.ChatMessageBinary] = Util.CreateChatMessage("<color=#FFFFFF>System",
                    p.world.WorldName,
                    p.world.WorldName,
                    1,
                    "\nYou are not registered. Please register at main menu for claim the new player bonus.");
                p.Send(ref cObjs);
         

            }


        }



        public string HandleCommandAutoLogin(Player p, string[] args)
        {



            if (args.Length < 1)
                return "Usage: /autologin";

     
            string userIpAddress = p.Data.LastIP; 

            var sql = pServer.GetSQL();

 
            using (var read = sql.FetchQuery($"SELECT * FROM players WHERE IP='{userIpAddress}'"))
            {
                if (!read.HasRows)
                    return "There is no account associated with your IP address!";


                while (read.Read())
                {
                    string name = (string)read["Name"];
                    string pass = (string)read["Pass"];

                    if (!name.StartsWith("LTPS_"))
                    {
            
                        var loginCmd = $"/login {name} {pass}";
                        return HandleCommandLogin(p, loginCmd.Split(' '));
                    }
                }
            }

            return "Couldn't login automatically, please enter your credentials manually.";
        }


        public void HandleRequestAI(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            p.Send(ref bObj);
        }

        public void HandleGetActiveWorlds(Player p)
        {
            if (p == null)
                return;

            BSONObject resp = new BSONObject("GAW");
            List<string> worldNames = new List<string>();
            List<int> playerCounts = new List<int>();

            foreach (var world in pServer.GetWorldManager().GetWorlds())
            {
                int pC = world.Players.Count;
                if (pC > 0)
                {
                    worldNames.Add(world.WorldName);
                    playerCounts.Add(pC);
                }
            }

            resp["W"] = worldNames;
            resp["WN"] = worldNames;
            resp["Ct"] = playerCounts;
            p.Send(ref resp);
        }

        public void HandleRequestAIp(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            p.Send(ref bObj);
          
        }

        public void HandleWearableUsed(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            int id = bObj["hBlock"];

            if (id < 0 || id >= ItemDB.ItemsCount())
                return;

            Item it = ItemDB.GetByID(id);

            bObj[MsgLabels.UserID] = p.Data.UserID;
            p.world.Broadcast(ref bObj, p);
        }

        public void HandleCollect(Player p, int colID)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            if (!p.world.collectables.ContainsKey(colID))
                return;

            BSONObject resp = new BSONObject();
            resp["ID"] = "C";
            resp["CollectableID"] = colID;

            WorldInterface.Collectable c = p.world.collectables[colID];
            resp["BlockType"] = c.item;
            resp["Amount"] = c.amt; // HACK
            resp["InventoryType"] = c.type;
            resp["PosX"] = c.posX;
            resp["PosY"] = c.posY;
            resp["IsGem"] = c.gemType > -1;
            resp["GemType"] = c.gemType < 0 ? 0 : c.gemType;

            if (c.gemType < 0)
            {
                p.inventoryManager.AddItemToInventory((BlockType)c.item, (InventoryItemType)c.type, c.amt);
            }
            else
            {
                int gemsToGive = 0;
                switch ((GemType)c.gemType)
                {
                    case GemType.Gem1:
                        gemsToGive = 1;
                        break;

                    case GemType.Gem2:
                        gemsToGive = 5;
                        break;

                    case GemType.Gem3:
                        gemsToGive = 20;
                        break;

                    case GemType.Gem4:
                        gemsToGive = 50;
                        break;

                    case GemType.Gem5:
                        gemsToGive = 100;
                        break;

                    default:
                        break;
                }

                p.Data.Gems += gemsToGive;
            }

            p.world.RemoveCollectable(colID, p);
            p.Send(ref resp);
        }

        public void HandleWearableRemoved(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            int id = bObj["hBlock"];

            if (id < 0 || id >= ItemDB.ItemsCount())
                return;

            Item it = ItemDB.GetByID(id);


            bObj[MsgLabels.UserID] = p.Data.UserID;
            p.world.Broadcast(ref bObj, p);
        }

        public void HandleTryToJoinWorldRandom(Player p)
        {
            List<WorldSession> worlds = new List<WorldSession>();

            foreach (var world in pServer.GetWorldManager().GetWorlds())
            {
                int pC = world.Players.Count;
                if (pC > 0)
                {
                    worlds.Add(world);
                }
            }
            if (worlds.Count > 0)
            {
                var w = worlds[new Random().Next(worlds.Count)];

                BSONObject bObj = new BSONObject();
                bObj["ID"] = "OoIP";
                bObj["IP"] = "prod.gamev85.portalworldsgame.com";
                bObj["WN"] = w.WorldName;

                p.Send(ref bObj);
            }
        }

      
        public void HandleRespawn(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            var w = p.world;

            BSONObject resp = new BSONObject();
            resp[MsgLabels.MessageID] = "UD";
            resp[MsgLabels.UserID] = p.Data.UserID;
            resp["x"] = w.SpawnPointX;
            resp["y"] = w.SpawnPointY;
            resp["DBl"] = 0;

            w.Broadcast(ref resp);
            p.Send(ref bObj);
        }

        public void HandleHitBlockWater(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            var w = p.world;

            int x = bObj["x"], y = bObj["y"];
            var tile = w.GetTile(x, y);

            BSONObject resp = new BSONObject("DB");

            if (tile != null)
            {
                if (tile.water.id <= 0)
                    return;
                if (p.world.lockWorldData != null && ((!p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID)) && p.Data.adminStatus != Player.AdminStatus.AdminStatus_Admin))
                {
                    //p.SelfChat("World is locked by " + pServer.GetNameFromUserID(w.lockWorldData.GetPlayerWhoOwnsLockId()));
                    return;
                }


                if (Util.GetSec() > tile.water.lastHit + 4)
                {
                    tile.water.damage = 0;
                }

                if (++tile.water.damage > 2)
                {
                    resp[MsgLabels.DestroyBlockBlockType] = (int)tile.water.id;
                    resp[MsgLabels.UserID] = p.Data.UserID;
                    resp["x"] = x;
                    resp["y"] = y;
                    w.Broadcast(ref resp);
                    if (p.Data.XP >= 11940000)
                    {
                        p.AddXP(0);

                    }
                    else
                    {
                        p.AddXP(300);
                    }
                    tile.water.id = 0;
                    tile.water.damage = 0;

                    double pX = x / Math.PI, pY = y / Math.PI;
                    
               }

                tile.water.lastHit = Util.GetSec();
            }
        }

        public void HandleHitBackground(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            var w = p.world;

            int x = bObj["x"], y = bObj["y"];
            var tile = w.GetTile(x, y);


            BSONObject resp = new BSONObject("DB");

            if (tile != null)
            {
                if (tile.bg.id <= 0)
                    return;



                if (p.world.lockWorldData != null && ((!p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID)) && !w.lockWorldData.GetIsOpen()) && p.Data.adminStatus != AdminStatus.AdminStatus_Admin)
                {
                    //p.SelfChat("World is locked by " + pServer.GetNameFromUserID(w.lockWorldData.GetPlayerWhoOwnsLockId()));
                    return;
                }

                if (!vurusSayaci.ContainsKey(p))
                {
                    vurusSayaci[p] = 0;
                    sonVurusZamani[p] = Util.GetSec();
                }


                double suankiZaman = Util.GetSec();


                if (suankiZaman - sonVurusZamani[p] >= 1.0)
                {
                    vurusSayaci[p] = 0;
                    sonVurusZamani[p] = suankiZaman;
                }


                vurusSayaci[p]++;



                if (vurusSayaci[p] > 9)
                {

                    BSONObject dc = new BSONObject("DR");
                    p.Send(ref dc);
                    return;
                }



                /*  if (p.inventoryManager.HasItemAmountInInventory((WorldInterface.BlockType)1018, InventoryItemType.Weapon, 1))
                    {
                        tile.bg.damage++;
                        tile.bg.damage++;
                 }*/


                if (p.Data.XP >= 12059999)
                {
                    p.AddXP(0);

                }
                else
                {
                    p.AddXP(2);
                }




                if (Util.GetSec() > tile.bg.lastHit + 4)
                {
                    tile.bg.damage = 0;
                }

                if (++tile.bg.damage > 2)
                {
                    resp[MsgLabels.DestroyBlockBlockType] = (int)tile.bg.id;
                    resp[MsgLabels.UserID] = p.Data.UserID;
                    resp["x"] = x;
                    resp["y"] = y;
                    w.Broadcast(ref resp);


                    tile.bg.id = 0;
                    tile.fg.damage = 0;
                    int gemCount = Util.rand.Next(1, 4); // Generate a random number between 1 and 3

                    double pX = x / Math.PI, pY = y / Math.PI;

                    for (int i = 0; i < gemCount; i++)


                         w.Drop(0, 1, pX - 0.1 + Util.rand.NextDouble(0, 0.2), pY - 0.1 + Util.rand.NextDouble(0, 0.2), 0, Util.rand.Next(2));

                }
           
                tile.bg.lastHit = Util.GetSec();
            }
        }


   

        private Dictionary<Player, int> vurusSayaci = new Dictionary<Player, int>();
        private Dictionary<Player, double> sonVurusZamani = new Dictionary<Player, double>();

        public void HandleHitBlock(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            var w = p.world;

            int x = bObj["x"], y = bObj["y"];
            var tile = w.GetTile(x, y);

            BSONObject resp = new BSONObject("DB");

            if (tile != null)
            {
                if (tile.fg.id <= 0 || tile.fg.id == 110 || tile.fg.id == 3)
                    return;




                if (p.world.lockWorldData != null && ((!p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID)) && !w.lockWorldData.GetIsOpen() && p.Data.adminStatus != AdminStatus.AdminStatus_Admin))
                {
                    //p.SelfChat("World is locked by " + pServer.GetNameFromUserID(w.lockWorldData.GetPlayerWhoOwnsLockId()));
                    return;
                }



                if (!vurusSayaci.ContainsKey(p))
                {
                    vurusSayaci[p] = 0;
                    sonVurusZamani[p] = Util.GetSec();
                    return;
                }


                double suankiZaman = Util.GetSec();



                if (suankiZaman - sonVurusZamani[p] >= 1.0)
                {
                    vurusSayaci[p] = 0;
                    sonVurusZamani[p] = suankiZaman;
                }


                vurusSayaci[p]++;



                if (vurusSayaci[p] > 9)
                {

                    BSONObject dc = new BSONObject("DR");
                    p.Send(ref dc);
                    return;
                }

  



                if (Util.GetSec() > tile.fg.lastHit + 4)
                {
                    tile.fg.damage = 0;
                }


                //if (tile.fg.id == 1421)
                // {
                //   tile.fg.damage++;
                // tile.fg.damage++;

                //  }

                //      if (p.inventoryManager.HasItemAmountInInventory((WorldInterface.BlockType)54, InventoryItemType.Weapon, 1))
                //        {
                //    tile.fg.damage++;
                // tile.fg.damage++;
                //    }

                if (p.Data.XP >= 12059999)
                {
                    p.AddXP(0);

                }
                else
                {
                    p.AddXP(2);
                }



                if (++tile.fg.damage > 2)
                {
                    resp[MsgLabels.DestroyBlockBlockType] = (int)tile.fg.id;
                    resp[MsgLabels.UserID] = p.Data.UserID;
                    resp["x"] = x;
                    resp["y"] = y;
                    w.Broadcast(ref resp);

                    double pX = x / Math.PI, pY = y / Math.PI;
                    int gemCount = Util.rand.Next(1, 4); // Generate a random number between 1 and 3

                    if (tile.fg.id == (short)WorldInterface.BlockType.LockWorld)
                    {

                        if (w.lockWorldData.GetPlayerWhoOwnsLockId() == p.Data.UserID)
                        {
                            w.worldItems.Remove(w.lockWorldData);
                            w.lockWorldData = null;
                            w.Drop(tile.fg.id, 1, pX, pY, 0);
                            HandleCollect(p, w.colID);
                        }
                        else
                        {
                            return;
                        }
         
                    }



                    if (tile.fg.id == 796)
                    {
                        w.Drop(tile.fg.id, 1, pX, pY, 0);
                        HandleCollect(p, w.colID);
                    }

                    if (tile.fg.id == 1605)
                    {
                        w.Drop(tile.fg.id, 1, pX, pY, 0);
                        HandleCollect(p, w.colID);
                    }




                    for (int i = 0; i < gemCount; i++)

                        if (tile.fg.id == 1421)
                        {
                            w.Drop(0, 1, pX - 0.1 + Util.rand.NextDouble(0, 0.2), pY - 0.1 + Util.rand.NextDouble(0, 0.2), 0, Util.rand.Next(2));
 
                        }
                        else

                            w.Drop(0, 1, pX - 0.1 + Util.rand.NextDouble(0, 0.2), pY - 0.1 + Util.rand.NextDouble(0, 0.2), 0, Util.rand.Next(2));
                    tile.fg.id = 0;
                    tile.fg.damage = 0;
                    return;










                }

                tile.fg.lastHit = Util.GetSec();
            }


            

        }

        public void HandleSetBlockWater(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            var w = p.world;

            int x = bObj["x"], y = bObj["y"];
            BlockType blockType = (BlockType)bObj["BlockType"].int32Value;
            InventoryKey inventoryKey = new InventoryKey(blockType, InventoryItemType.BlockWater);
            Item it = ItemDB.GetByID((int)blockType);

            var invIt = p.inventoryManager.HasItemAmountInInventory(inventoryKey);
            if (!invIt)
                return;

            if (it.type != 3)
                return;

            bObj["U"] = p.Data.UserID;


            var t = w.GetTile(x, y);
            t.water.id = (short)blockType;
            t.water.damage = 0;
            t.water.lastHit = 0;

            w.Broadcast(ref bObj);

            p.inventoryManager.RemoveItemsFromInventory(inventoryKey);
        }


        public void HandleSetBlock(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            var w = p.world;

            int x = bObj["x"], y = bObj["y"];
            short blockType = (short)bObj["BlockType"];
            Item it = ItemDB.GetByID(blockType);

      

            var invIt = p.inventoryManager.HasItemAmountInInventory((BlockType)blockType, (InventoryItemType)it.type);
            if (!invIt)
                return;

            if (p.world.lockWorldData != null && ((!p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID)) && !w.lockWorldData.GetIsOpen()) && p.Data.adminStatus != AdminStatus.AdminStatus_Admin)
            {
               // p.SelfChat("World is locked by " + pServer.GetNameFromUserID(w.lockWorldData.GetPlayerWhoOwnsLockId()));
                return;
            }

            bObj["U"] = p.Data.UserID;
            if(it.type==3) bObj[MsgLabels.MessageID] = MsgLabels.Ident.SetBlockWater;
            bool suc = p.world.SetBlock(x, y, blockType, p);
            if (suc)
            {
                p.world.Broadcast(ref bObj);
                if (blockType == (short)BlockType.LockWorld)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "LockWorldData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = true;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["playerWhoOwnsLockId"] = p.Data.UserID;
                    bbobj["WiB"]["playerWhoOwnsLockName"] = p.Data.Name;
                    bbobj["WiB"]["playersWhoHaveAccessToLock"] = new List<string>();
                    bbobj["WiB"]["playersWhoHaveMinorAccessToLock"] = new List<string>();
                    bbobj["WiB"]["isOpen"] = false;
                    bbobj["WiB"]["punchingAllowed"] = false;
                    bbobj["WiB"]["creationTime"] = DateTime.UtcNow;
                    bbobj["WiB"]["lastActivatedTime"] = DateTime.UtcNow;
                    bbobj["WiB"]["isBattleOn"] = false;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.Door)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "DoorData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.BarnDoor)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "BarnDoorData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.CastleDoor)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "CastleDoorData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.ScifiDoor)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "ScifiDoorData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = true;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.GlassDoor)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "GlassDoorData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.GlassDoorTinted)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "GlassDoorTintedData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.DungeonDoor || blockType == (short)BlockType.DungeonDoorWhite)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "DungeonDoorData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }/*
                else
                if (blockType == (short)BlockType.DoorFactionLight)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "DoorFactionLightData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }
                else
                if (blockType == (short)BlockType.DoorFactionDark)
                {
                    BSONObject bbobj = new BSONObject();
                    bbobj["ID"] = "WIU";
                    bbobj["WiB"] = new BSONObject();
                    bbobj["WiB"]["class"] = "DoorFactionDarkData";
                    bbobj["WiB"]["itemId"] = w.itemIndex;
                    bbobj["WiB"]["blockType"] = (int)blockType;
                    bbobj["WiB"]["animOn"] = false;
                    bbobj["WiB"]["direction"] = 0;
                    bbobj["WiB"]["anotherSprite"] = true;
                    bbobj["WiB"]["damageNow"] = false;
                    bbobj["WiB"]["isLocked"] = true;
                    bbobj["x"] = x;
                    bbobj["y"] = y;
                    bbobj["ItsNewWIB"] = true;
                    w.Broadcast(ref bbobj);
                }*/
                p.inventoryManager.RemoveItemsFromInventory((BlockType)blockType, 0);
             
            }
        }

        public void HandleSetBackgroundBlock(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            var w = p.world;

            if (p.world.lockWorldData != null && ((!p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID)) && !w.lockWorldData.GetIsOpen()) && p.Data.adminStatus != AdminStatus.AdminStatus_Admin)
            {
                //p.SelfChat("World is locked by " + pServer.GetNameFromUserID(w.lockWorldData.GetPlayerWhoOwnsLockId()));
                return;
            }

            int x = bObj["x"], y = bObj["y"];
            short blockType = (short)bObj["BlockType"];
            Item it = ItemDB.GetByID(blockType);

            var invIt = p.inventoryManager.HasItemAmountInInventory((BlockType)blockType, (InventoryItemType)1);
            if (!invIt)
                return;

            bObj["U"] = p.Data.UserID;


            var t = w.GetTile(x, y);
            t.bg.id = blockType;
            t.bg.damage = 0;
            t.bg.lastHit = 0;

            w.Broadcast(ref bObj);

            p.inventoryManager.RemoveItemsFromInventory((BlockType)blockType, (InventoryItemType)1);
        }


        List<int> illegalItemIDs = new List<int> { 413, 416, 784, 796, 882, 1038, 1085, 1131, 1135, 2212, 2334, 2335, 2356, 2359, 2381, 2632, 3097, 3529, 3578, 3606, 3769, 3777, 3815, 3816, 3824, 3890, 3891, 3892, 3920, 4162, 4170, 4171, 4266, 4267, 4369, 4370, 4371, 4372, 4435, 4471, 4472, 4498, 4499, 4561, 4647, 4700, 4705, 4706, 4707, 4708, 4709, 4710, 4729, 4788, 4789, 4790, 4792, 4802, 4823, 4860, 4861, 4906, 4907, 4908, 4909, 4910 , 2096 , 4999};

        public void HandleDropItem(Player p, BSONObject bObj)
        {
          if (p == null)
             return;

            if (p.world == null)
            return;

   


            BSONObject dObj = bObj["dI"] as BSONObject;

                  BlockType blockType = (BlockType)dObj["BlockType"].int32Value;
                 int amount = dObj["Amount"];
                 int type = dObj["InventoryType"];
              double x = Convert.ToDouble(bObj["x"].int32Value) / Math.PI;
             double y = Convert.ToDouble(bObj["y"].int32Value) / Math.PI;

            bool IsIllegalItem(int itemID)
            {
                return illegalItemIDs.Contains(itemID);
            }


            if (IsIllegalItem((int)blockType)) 
            {

                p.SelfChat("You cant drop this untradeable item!");
                return;
            }

            var invItem = p.inventoryManager.HasItemAmountInInventory(blockType, (InventoryItemType)type, (short)amount);

                  if (!invItem)
                   return;

             p.inventoryManager.RemoveItemsFromInventory(blockType, (InventoryItemType)type, (short)amount);
            p.SendRemoveItemInventory(blockType, (InventoryItemType)type, amount);
              p.world.Drop((int)blockType, amount, x - 0.1 + Util.rand.NextDouble(0, 0.2), y - 0.1 + Util.rand.NextDouble(0, 0.2), type, -1);

        }
        public void HandleTrashItem(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;
            BSONObject dObj = bObj["dI"] as BSONObject;

            BlockType blockType = (BlockType)dObj["BlockType"].int32Value;
            int amount = dObj["Amount"];
            int type = dObj["InventoryType"];

            var invItem = p.inventoryManager.HasItemAmountInInventory(blockType, (InventoryItemType)type, (short)amount);
          
            if (!invItem)
                return;


            p.inventoryManager.RemoveItemsFromInventory(blockType, (InventoryItemType)type, (short)amount);
            p.SendRemoveItemInventory(blockType, (InventoryItemType)type, amount);

        }


        public void HandleMovePlayer(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;

            if (bObj.ContainsKey("x") &&
                bObj.ContainsKey("y") &&
                bObj.ContainsKey("a") &&
                bObj.ContainsKey("d") &&
                bObj.ContainsKey("t"))

            {
                p.Data.PosX = bObj["x"].doubleValue;
                p.Data.PosY = bObj["y"].doubleValue;

                p.Data.Anim = bObj["a"];
                p.Data.Dir = bObj["d"];
                bObj["U"] = p.Data.UserID;

                if (bObj.ContainsKey("tp"))
                    bObj.Remove("tp");
                if (!p.Data.adminWantsToGoGhostMode) p.world.Broadcast(ref bObj, p);
            }
        }









        public void HandleSyncTime(FeatherClient client)
        {
            BSONObject resp = new BSONObject(MsgLabels.Ident.SyncTime);
            resp[MsgLabels.MessageID] = MsgLabels.Ident.SyncTime;
            resp[MsgLabels.TimeStamp] = Util.GetKukouriTime();
            resp[MsgLabels.SequencingInterval] = 60;

            client.Send(resp);
        }

        public void HandleOrbChange(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;
            var w = p.world;

            if (p.world.lockWorldData != null && ((!w.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID)) && !w.lockWorldData.GetIsOpen()))
            {
                p.SelfChat("You cant use orb at worlds which you dont own!");
                return;
            }
            else
            {
                int orb = bObj["bgT"].int32Value;
                BlockType blockType = Config.getOrbBlockType(orb);
                bool invItem = p.inventoryManager.HasItemAmountInInventory(blockType, InventoryItemType.Consumable);
                if (invItem)
                {
                    p.inventoryManager.RemoveItemsFromInventory(blockType, InventoryItemType.Consumable, 1);
                    p.world.BackGroundType = (LayerBackgroundType)orb;
                }
                p.SendRemoveItemInventory(blockType, InventoryItemType.Consumable, 1);
                BSONObject wObj = new BSONObject();
                wObj["ID"] = "ChangeBackground";
                wObj["bgT"] = orb;
                wObj["U"] = p.Data.UserID;
                p.world.Broadcast(ref wObj);
            }
            
        }
        public void HandleWeatherChange(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;
            var w = p.world;

            if (p.world.lockWorldData != null && ((!w.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID)) && !w.lockWorldData.GetIsOpen()) && p.Data.adminStatus != AdminStatus.AdminStatus_Admin)
            {
                p.SelfChat("You cant use orb at worlds which you dont own!");
            }
            else
            {
                int weather = bObj["wto"].int32Value;
                BlockType blockType = Config.getWeatherBlockType(weather);
                bool invItem = p.inventoryManager.HasItemAmountInInventory(blockType, InventoryItemType.Consumable);
                if (invItem)
                {
                    p.inventoryManager.RemoveItemsFromInventory(blockType, InventoryItemType.Consumable, 1);
                    p.world.WeatherType = (WeatherType)weather;
                }
                p.SendRemoveItemInventory(blockType, InventoryItemType.Consumable, 1);
                BSONObject wObj = new BSONObject();
                wObj["ID"] = "CWWoq";
                wObj["wto"] = weather;
                wObj["U"] = p.Data.UserID;
                p.world.Broadcast(ref wObj);
            }

        }
        public void HandleSummon(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;
            bool perm = p.world.CanSummon(p, bObj["U"].stringValue);
            if (!perm & p.Data.adminStatus != AdminStatus.AdminStatus_Admin && p.Data.adminStatus != AdminStatus.AdminStatus_Moderator) return;

            //var pos = Config.ConvertWorldPointToMapPoint(Convert.ToSingle(p.Data.PosX), Convert.ToSingle(p.Data.PosY));
            BSONObject mObj = new BSONObject();
            mObj["ID"] = "WP";
            mObj["U"] = bObj["U"].stringValue;
            mObj["PX"] = Convert.ToInt32((float)p.Data.PosX * Math.PI);
            mObj["PY"] = Convert.ToInt32((float)p.Data.PosY * Math.PI);
            Player player = p.world.Players.Find(pl => pl.Data.UserID == bObj["U"].stringValue);
            if (player.Data.UserID == bObj["U"].stringValue)
            {
                    player.Send(ref mObj);
                //p.world.RemovePlayer(player);
            }


        }
        public void HandleKick(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;
            bool perm = p.world.CanKick(p, bObj["U"].stringValue);
            if (!perm & p.Data.adminStatus != AdminStatus.AdminStatus_Admin && p.Data.adminStatus != AdminStatus.AdminStatus_Moderator) return;

            BSONObject mObj = new BSONObject();           
            mObj["ID"] = "KPl";
            mObj["BPl"] = 0;
            mObj["WN"] = p.world.WorldName;
            mObj["BanState"] = "World";
            mObj["T"] = DateTime.UtcNow.Ticks;
            mObj["BanFromGameReasonValue"] = "Scamming";
            //mObj["Idx"] = 0;
            Player player = p.world.Players.Find(pl => pl.Data.UserID == bObj["U"].stringValue);
            if (player == null)
                return;

            if (player == null)
                return;
            if (player.Data.UserID == bObj["U"].stringValue)
            {
          
                    player.Send(ref mObj);
                //p.world.RemovePlayer(player);
            }
        }
        public void HandleBan(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;
            bool perm = p.world.CanBan(p, bObj["U"].stringValue);
            if (!perm & p.Data.adminStatus != AdminStatus.AdminStatus_Admin) return;


    

            BSONObject mObj = new BSONObject();
            mObj["ID"] = "KPl";
            mObj["BPl"] = 1;
            mObj["WN"] = p.world.WorldName;
            mObj["BanState"] = "Universal";
            mObj["T"] = 641410765070000000;
            mObj["BanFromGameReasonValue"] = "Breaking the Game Rules";


            //mObj["Idx"] = 0;
            Player player = p.world.Players.Find(pl => pl.Data.UserID == bObj["U"].stringValue);
            if (player == null)
                return;

            if (player == null)
                return;
            if (player.Data.UserID == bObj["U"].stringValue)
            {

                if (p.Data.adminStatus != AdminStatus.AdminStatus_Admin) return;
                {
                    string filePath = "banned.txt";
                    string content = "\n " + player.Data.Name;
                    string contentWithNewLine = content + Environment.NewLine;
                    File.AppendAllText(filePath, contentWithNewLine);

                    player.Send(ref mObj);
                    player.world.banList.Add(player.Data.UserID, DateTime.UtcNow.AddHours(1).Ticks);

                    p.SelfChat("User has been banned successfully! Banned wrongly? Use /unban to unban the player.");
                    //p.world.RemovePlayer(player);
                }


            }
           
        }
        public void HandleWorldItemUpdate(Player p, BSONObject bObj)
        {
            if (p == null)
                return;

            if (p.world == null)
                return;
            WorldItemBase worldItem = p.world.FindItemBaseWithID(bObj["WiB"]["itemId"].int32Value);
            if(worldItem == null) return;
            if(worldItem.blockType == BlockType.LockWorld)
            {
                var worldLock = (LockWorldData)worldItem;

                if (p.Data.UserID != worldLock.GetPlayerWhoOwnsLockId()) return;
                if (p.Data.Name != worldLock.GetPlayerWhoOwnsLockName())worldLock.SetPlayerWhoOwnsLockName(p.Data.Name);
                foreach (string str in bObj["WiB"]["playersWhoHaveAccessToLock"].stringListValue)
                {
                    if (!IsAccessFormatValid(str)) return;
                }
                foreach (string str in bObj["WiB"]["playersWhoHaveMinorAccessToLock"].stringListValue)
                {
                    if (!IsAccessFormatValid(str)) return;
                }
                worldLock.SetPlayersWhoHaveAccessToLock(bObj["WiB"]["playersWhoHaveAccessToLock"].stringListValue);
                worldLock.SetPlayersWhoHaveMinorAccessToLock(bObj["WiB"]["playersWhoHaveMinorAccessToLock"].stringListValue);
                worldLock.SetIsOpen(bObj["WiB"]["isOpen"].boolValue);
                worldLock.SetIsPunchingAllowed(bObj["WiB"]["punchingAllowed"].boolValue);
                BSONObject wObj = new BSONObject();
                wObj["ID"] = "WIU";
                wObj["WiB"] = p.world.lockWorldData.GetAsBSON();
                wObj["x"] = p.world.lockWorldData.x;
                wObj["y"] = p.world.lockWorldData.y;
                wObj["PT"] = 1;
                wObj["U"] = p.Data.UserID;
                p.world.Broadcast(ref wObj);

            }
        }
        public void HandleRecentWorlds(Player p) 
        {
            if(p==null) return;
            else
            {
                var worldsData = p.GetRecentWorlds();

                BSONObject wObj = new BSONObject("GRW");
                wObj["WN"] = worldsData[0];
                wObj["W"] = worldsData[1];
                List<int> count = new List<int>();
                var wmgr = pServer.GetWorldManager();
                foreach (string worldName in worldsData[0])
                {
                    if (wmgr.GetByName(worldName) != null)
                    {
                        count.Add(wmgr.GetByName(worldName).Players.Count);
                    }
                    else
                    {
                        count.Add(0);
                    }
                }
                wObj["Ct"] = count;
                p.Send(ref wObj);
            }

        }

        public void HandleDailyBonus(Player p, BSONObject bObj)
        {
            Random random = new Random();
            int randomNumber = random.Next(1, 9);

            BSONObject response = new BSONObject();
            response[MsgLabels.MessageID] = "RDB";
            response["RB"] = randomNumber;

            p.Send(ref response);
        }
        public void HandleAdminTeleport(Player p, BSONObject bObj)
        {
            if(p==null) return;
            if(p.world==null)return;
            if(p.Data.adminStatus != AdminStatus.AdminStatus_Admin && p.Data.adminStatus != AdminStatus.AdminStatus_Moderator) 
            {
                return;
            }
            BSONObject mObj = new BSONObject();
            mObj["ID"] = "WP";
            mObj["U"] = p.Data.UserID;
            mObj["PX"] = bObj["1x"];
            mObj["PY"] = bObj["1y"];
            p.Send(ref mObj);
        }
        public void HandleAdminKill(Player p, BSONObject bObj)
        {
            if (p == null) return;
            if (p.world == null) return;
            if (p.Data.adminStatus != AdminStatus.AdminStatus_Admin && p.Data.adminStatus != AdminStatus.AdminStatus_Moderator)
            {
                BSONObject mObj = new BSONObject();
                mObj["ID"] = "UD";
                mObj["U"] = bObj["U"].stringValue;

                mObj["x"] = p.world.SpawnPointX;
                mObj["y"] = p.world.SpawnPointY;

                if (p.Data.UserID == bObj["U"].stringValue)
                {



                }


            }

        }

        public void AHGetCCgy(Player p, BSONObject bObj)
        {

            BSONObject packet = new BSONObject();
            packet["ID"] = "AHGetCCgy";
            packet["AHIv"] = bObj["AHIv"];
            packet["S"] = true;

            p.Send(ref packet);
        }
        public void HandleAdminUnderCover(Player p, BSONObject bObj)
        {
            if (p == null) return;
            if (p.world == null) return;
            if (p.Data.adminStatus != AdminStatus.AdminStatus_Admin && p.Data.adminStatus != AdminStatus.AdminStatus_Moderator)
            {
                return;
            }
            if (bObj.ContainsKey("AdminsNameOnUndercoverModeValue"))
            {

                BSONObject mObj = new BSONObject();
                if (!p.Data.adminWantsToGoUndercoverMode)
                {
                    p.Data.RealName = p.Data.Name;
                    p.Data.Name = bObj["AdminsNameOnUndercoverModeValue"].stringValue;

                    mObj["ID"] = "AdminSetUndercoverMode";
                    mObj["U"] = p.Data.UserID;

                    mObj["AdminsRealName"] = p.Data.Name;
                    mObj["AdminsRealNameCountryCode"] = 999;
                    mObj["AdminSetUndercoverModeValue"] = true;
                    mObj["AdminOrMod"] = p.Data.adminStatus == AdminStatus.AdminStatus_Admin;
                }
                else if (p.Data.adminWantsToGoUndercoverMode)
                {
                    p.Data.Name = p.Data.RealName;

                    mObj["ID"] = "AdminSetUndercoverMode";
                    mObj["U"] = p.Data.UserID;

                    mObj["AdminsRealName"] = p.Data.RealName;
                    mObj["AdminsRealNameCountryCode"] = 999;
                    mObj["AdminSetUndercoverModeValue"] = false;
                    mObj["AdminOrMod"] = p.Data.adminStatus == AdminStatus.AdminStatus_Admin;
                    
                }
                p.Send(ref mObj);
            }

        }



        public void HandleAuctionHouseGetItems(Player p, BSONObject bObj)
        {
            if (p == null) return;
            if (p.world == null) return;
            PWEHelper pweHelper = pServer.GetPWEHelper();
            int IK = bObj["IK"].int32Value;
            int index = bObj["Idx"].int32Value;
            int itemID = IK & 16777215;
            InventoryItemType inventoryItemType = (InventoryItemType)(IK >> 24);
            List<PWEShopResult> pweItems = pweHelper.GetPWEItemsByInventoryKey(itemID, inventoryItemType, index);
            BSONObject wObj = new BSONObject("AHGetItems");
            wObj["IK"] = IK;
            List<long> longValueList = new List<long>();
            List<string> stringValueList = new List<string>();
            List<int> intValueList = new List<int>();
            foreach (PWEShopResult pweItem in pweItems)
            {
                longValueList.Add(pweItem.creationTime);
                longValueList.Add(pweItem.expirationTime);
                stringValueList.Add(pweItem.sellerID);
                stringValueList.Add("");
                intValueList.Add(pweItem.amount);
                intValueList.Add(pweItem.price);
                intValueList.Add(0);
            }
            wObj["AHLv"] = longValueList;
            wObj["AHSv"] = stringValueList;
            wObj["AHIv"] = intValueList;
            wObj["S"] = true;
            int count = ((pweHelper.GetPWEItemsByInventoryKey(itemID, inventoryItemType).Count + 19) / 20);
            wObj["Idx"] = count;
            p.Send(ref wObj);

        }
        public void HandleAuctionHouseBuyItem(Player p, BSONObject bObj)
        {
            if (p == null) return;
            if (p.world == null) return;
            PWEHelper pweHelper = pServer.GetPWEHelper();
            long ticks = bObj["T"];
            int IK = bObj["IK"];
            PWEShopResult pweItem = pweHelper.GetPWEShopResultByCreationTicks(ticks);
            if (pweItem == null) return;
            if (pweItem.sold==true) return;
            if (IK == 67110960 || IK == 3 || IK == 344 || IK == 343 || IK == 67109902 || IK == 83886811 || IK == 83888176 || IK == 83887118 || IK == 67109595)
            {
                BSONObject ER = new BSONObject("AHBuyMsg");
                ER["ER"] = "Invalid packet";
                ER["S"] = false;
                p.Send(ref ER);
                return;
            }
            if (p.Data.Coins < pweItem.price) return;
            BSONObject wObj = new BSONObject("AHBuyMsg");
            wObj["IK"] = Config.BlockTypeAndInventoryItemTypeToInt((BlockType)pweItem.itemID, pweItem.inventoryItemType);
            wObj["T"] = ticks;
            wObj["x"] = 40;
            wObj["y"] = 30;
            wObj["CDat"] = new BSONObject();
            wObj["CDat"]["CollectableID"] = 0;
            wObj["CDat"]["BlockType"] = pweItem.itemID;
            wObj["CDat"]["Amount"] = pweItem.amount ;
            wObj["CDat"]["InventoryType"] = (int)pweItem.inventoryItemType;
            wObj["CDat"]["PosX"] = Convert.ToDouble(0);
            wObj["CDat"]["PosY"] = Convert.ToDouble(0);
            wObj["CDat"]["IsGem"] = false;
            wObj["CDat"]["GemType"] = 0;
            wObj["S"] = true;
            p.Send(ref wObj);
            p.RemoveCoins(pweItem.price);
            p.inventoryManager.AddItemToInventory((WorldInterface.BlockType)pweItem.itemID, pweItem.inventoryItemType, (short)pweItem.amount);
           
        }

        public void AHGetPItems(Player p, BSONObject bObj)
        {
            BSONObject packet = new BSONObject();
            packet["ID"] = "AHGetPItems";
            packet["S"] = true;
            packet["AHCv"] = 0.10000000149011612;
            p.Send(ref packet);
            return;
        }

        public void HandleAuctionHouseSellItem(Player p, BSONObject bObj)
        {
            if (p == null || p.world == null) return;

            PWEHelper pweHelper = pServer.GetPWEHelper();

            // Extracting necessary information from bObj
            InventoryKey inventoryKey = Config.IntToInventoryKey(bObj["IK"].int32Value);
            int amount = bObj["Amt"].int32Value;
            int price = bObj["BCAmt"].int32Value;

            // Creating a PWEShopResult based on the item being sold
            long creationTicks = DateTime.UtcNow.Ticks;
            long expiryTicks = DateTime.UtcNow.AddDays(700).Ticks;
            pweHelper.CreatePWEShopResult(price, (int)inventoryKey.blockType, inventoryKey.itemType, "1", creationTicks, expiryTicks, amount);

            // Sending confirmation to the player
            bObj["S"] = true;
            p.Send(ref bObj);

            // Deducting coins from the player
            p.RemoveCoins(price);

            // Adding the sold item to the player's inventory
       
            p.inventoryManager.AddItemToInventory((WorldInterface.BlockType)inventoryKey.blockType, inventoryKey.itemType, (short)amount);
        }



        public void HandleAuctionHouseGetItemHistory(Player p, BSONObject bObj)
        {

            if (p == null) return;
            if (p.world == null) return;
            BSONObject wObj = new BSONObject("AHhE");
            wObj["IK"] = bObj["IK"].int32Value;
            wObj["S"] = true;
            p.Send(ref wObj);
            //p.RemoveCoins(pweItem.price);
            //p.inventoryManager.AddItemToInventory((WorldInterface.BlockType)pweItem.itemID, pweItem.inventoryItemType, (short)pweItem.amount);
        }
        public void HandleAuctionHouseGetPlayerItemListing(Player p, BSONObject bObj)
        {
            if (p == null) return;
            if (p.world == null) return;
            if (p.Data.adminStatus != AdminStatus.AdminStatus_Admin) return;
            BSONObject wObj = new BSONObject("AHGetPItems");
            wObj["S"] = true;
            p.Send(ref wObj);
            //p.RemoveCoins(pweItem.price);
            //p.inventoryManager.AddItemToInventory((WorldInterface.BlockType)pweItem.itemID, pweItem.inventoryItemType, (short)pweItem.amount);
        }
        public static bool IsAccessFormatValid(string input)
        {
            string pattern = @"^\w+;\w+$";

            Regex regex = new Regex(pattern);

            return regex.IsMatch(input);
        }
        public delegate void LogDelegate(string message);

        public static void ReadBSON(BSONObject SinglePacket, string Parent = "", LogDelegate Log = null)
        {
            if (Log == null)
            {
                Log = Util.Log;
            }
            foreach (string Key in SinglePacket.Keys)
            {
                try
                {

                    BSONValue Packet = SinglePacket[Key];
                    if (Key == "ID" && Packet.stringValue == "p") return;
                    switch (Packet.valueType)
                    {
                        case BSONValue.ValueType.String:
                            Log($"{Parent} = {Key} | {Packet.valueType} = {Packet.stringValue}");
                            break;
                        case BSONValue.ValueType.Boolean:
                            Log($"{Parent} = {Key} | {Packet.valueType} = {Packet.boolValue}");
                            break;
                        case BSONValue.ValueType.Int32:
                            Log($"{Parent} = {Key} | {Packet.valueType} = {Packet.int32Value}");
                            break;
                        case BSONValue.ValueType.Int64:
                            Log($"{Parent} = {Key} | {Packet.valueType} = {Packet.int64Value}");
                            break;
                        case BSONValue.ValueType.Binary: // BSONObject
                            try
                            {
                                Log($"{Parent} = {Key} | {Packet.valueType} = {Packet.binaryValue}");
                                ReadBSON(SimpleBSON.Load(Packet.binaryValue), Key);
                            }
                            catch
                            {
                                Log($"{Parent} = {Key} | {Packet.valueType} = [{BitConverter.ToString(Packet.binaryValue)}]");
                            }
                            break;
                        case BSONValue.ValueType.Double:
                            Log($"{Parent} = {Key} | {Packet.valueType} = {Packet.doubleValue}");
                            break;
                        case BSONValue.ValueType.Array:
                            string bamboom = $"{Parent} = {Key} | {Packet.valueType} = " + "[" + string.Join(", ", Packet.stringListValue) + "]";
                            Log(bamboom);
                            break;
                        case BSONValue.ValueType.UTCDateTime:
                            Log($"{Parent} = {Key} | {Packet.valueType} = {Packet.dateTimeValue}");
                            break;
                        default:
                            Log($"{Parent} = {Key} | {Packet.valueType}");
                            ReadBSON((BSONObject)Packet, Key, Log);
                            //Log(BitConverter.ToString(ObjectToByteArray(((Object)Packet))));

                            break;
                    }
                }
                catch (Exception ee)
                {
                    Console.WriteLine(ee);
                }
            }
        }
    }
}
