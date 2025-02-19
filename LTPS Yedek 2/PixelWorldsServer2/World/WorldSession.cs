using System;
using System.Collections.Generic;
using System.Text;
using PixelWorldsServer2.Networking.Server;
using System.IO;
using Discord.Net;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Kernys.Bson;
using PixelWorldsServer2.DataManagement;
using System.Linq;
using static PixelWorldsServer2.World.WorldInterface;
using System.Threading;
using System.Runtime.CompilerServices;
using PixelWorldsServer2.ItemsData.Door;
using BasicTypes;

namespace PixelWorldsServer2.World
{
    public class WorldSession
    {
        private PWServer pServer = null;
        private byte version = 0x1;
        private List<Player> players = new List<Player>();
        public Dictionary<int, Collectable> collectables = new Dictionary<int, Collectable>();
        public int colID = 0;
        public string WorldID = "0";
        public short SpawnPointX = 42, SpawnPointY = 30;
        public string WorldName = string.Empty;
        public WorldInterface.WeatherType WeatherType = WorldInterface.WeatherType.None;
        public WorldInterface.LayerBackgroundType BackGroundType;
        private WorldTile[,] tiles = null;
        public List<WorldItemBase> worldItems = new List<WorldItemBase>();
        public int itemIndex = 0;
        public int GetSizeX() => tiles.GetUpperBound(0) + 1;
        public int GetSizeY() => tiles.GetUpperBound(1) + 1;

        public LockWorldData lockWorldData;
        public List<Player> Players => players;
        public Dictionary<string, long> banList = new Dictionary<string, long>();

        // New seed-related properties
        public Dictionary<int, SeedData> seeds = new Dictionary<int, SeedData>();

        public static bool WorldExists(string worldName)
        {
            string path = $"maps/{worldName}.map";
            return File.Exists(path);
        }
        public void AddPlayer(Player p)
        {


            if (HasPlayer(p) == -1)
                players.Add(p);

            p.world = this;

            Save();
        }

     
        public int HasPlayer(Player p)
        {
            for (int i = 0; i < players.Count; i++)
            {

              

                if (p.Data.UserID == players[i].Data.UserID)
                    return i;

            }

            return -1;
        }
        public int HasPlayer(string p)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (p == players[i].Data.UserID)
                    return i;

            }

            return -1;
        }
        public void RemovePlayer(Player p)
        {
            int idx = HasPlayer(p);

            if (idx >= 0)
                players.RemoveAt(idx);
            p.world = null;
            
        }

        public void RemoveCollectable(int colID, Player toIgnore = null)
        {
            collectables.Remove(colID);
            BSONObject bObj = new BSONObject("RC");
            bObj["CollectableID"] = colID;
            Broadcast(ref bObj, toIgnore);

        }


        public void Broadcast(ref BSONObject bObj, params Player[] ignored) // ignored player can be used to ignore packet being sent to player itself.
        {
            foreach (var p in players)
            {
                if (ignored.Contains(p))
                    continue;

                p.Send(ref bObj);
            }
        }

        public void Drop(int id, int amt, double posX, double posY, int type, int gem = -1)
        {
            int cId = ++colID;
            BSONObject cObj = new BSONObject("nCo");
            cObj["CollectableID"] = cId;
            cObj["BlockType"] = id;
            cObj["Amount"] = amt;
            cObj["InventoryType"] = type;

            Collectable c = new Collectable();
            c.amt = (short)amt;
            c.item = (short)id;
            c.posX = posX * Math.PI;
            c.posY = posY * Math.PI;
            c.gemType = (short)gem;
            c.type = (short)type;

            cObj["PosX"] = c.posX;
            cObj["PosY"] = c.posY;
            cObj["IsGem"] = c.gemType > -1;
            cObj["GemType"] = c.gemType < 0 ? 0 : c.gemType;

            collectables[cId] = c;

            Broadcast(ref cObj);
        }

        public WorldSession(PWServer pServer, string worldName = "")
        {
            if (worldName == "")
                return;

            // load from SQL and File, if it doesn't exist, then generate.
            // first retrieve worldID, name, metadata... if fail, then generate world.
            this.pServer = pServer;
            string path = $"maps/{worldName}.map";

            if (!File.Exists(path))
            {
#if DEBUG
                Util.Log("Generating new world with name: " + worldName);
#endif
                // generate world
                Generate(worldName);
                return;
            }

            Util.Log("Attempting to load world from DB...");
            Deserialize(File.ReadAllBytes(path));
            this.WorldName = worldName;
        }

        public void Generate(string name)
        {
            // first, add new entry to sql:
            // todo filter the name from bad shit b4 release...
            SpawnPointX = (short)(1 + new Random().Next(79));
            WorldName = name;

            SetupTerrain();
        }

        public void Save()
        {
            string path = $"maps/{WorldName}.map";

            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x4); // version

                for (int y = 0; y < GetSizeY(); y++)
                {
                    for (int x = 0; x < GetSizeX(); x++)
                    {
                        var tile = GetTile(x, y);

                        ms.Write(BitConverter.GetBytes(tile.fg.id));
                        ms.Write(BitConverter.GetBytes(tile.bg.id));
                        ms.Write(BitConverter.GetBytes(tile.water.id));
                        ms.Write(BitConverter.GetBytes(tile.wire.id));
                    }
                }

                ms.Write(BitConverter.GetBytes(collectables.Values.Count));
                for (int i = 0; i < collectables.Values.Count; i++)
                {
                    var col = collectables.ElementAt(i).Value;
                    ms.Write(BitConverter.GetBytes(col.item));
                    ms.Write(BitConverter.GetBytes(col.amt));
                    ms.Write(BitConverter.GetBytes(col.posX));
                    ms.Write(BitConverter.GetBytes(col.posY));
                    ms.Write(BitConverter.GetBytes(col.gemType));
                    ms.Write(BitConverter.GetBytes(col.type));
                }
                ms.Write(BitConverter.GetBytes((int)BackGroundType));
                ms.Write(BitConverter.GetBytes((int)WeatherType));
                if (worldItems.Count > 0)
                {
                    BSONObject dobj = new BSONObject();
                    foreach (WorldItemBase item in worldItems)
                    {

                        if (item.blockType == BlockType.LockWorld)
                        {
                            dobj["W " + lockWorldData.x + " " + lockWorldData.y] = lockWorldData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.Door)
                        {
                            DoorData doorData = (DoorData)item;
                            dobj["W " + doorData.x + " " + doorData.y] = doorData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.CastleDoor)
                        {
                            CastleDoorData scifidoorData = (CastleDoorData)item;
                            dobj["W " + scifidoorData.x + " " + scifidoorData.y] = scifidoorData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.ScifiDoor)
                        {
                            ScifiDoorData scifidoorData = (ScifiDoorData)item;
                            dobj["W " + scifidoorData.x + " " + scifidoorData.y] = scifidoorData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.BarnDoor)
                        {
                            BarnDoorData doorData = (BarnDoorData)item;
                            dobj["W " + doorData.x + " " + doorData.y] = doorData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.GlassDoor)
                        {
                            GlassDoorData doorData = (GlassDoorData)item;
                            dobj["W " + doorData.x + " " + doorData.y] = doorData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.GlassDoorTinted)
                        {
                            GlassDoorTintedData doorData = (GlassDoorTintedData)item;
                            dobj["W " + doorData.x + " " + doorData.y] = doorData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.DungeonDoor || item.blockType == BlockType.DungeonDoorWhite)
                        {
                            DungeonDoorData doorData = (DungeonDoorData)item;
                            dobj["W " + doorData.x + " " + doorData.y] = doorData.GetAsBSON();
                        }/*
                        else
                        if (item.blockType == BlockType.DoorFactionDark)
                        {
                            DoorFactionDarkData doorData = (DoorFactionDarkData)item;
                            dobj["W " + doorData.x + " " + doorData.y] = doorData.GetAsBSON();
                        }
                        else
                        if (item.blockType == BlockType.DoorFactionLight)
                        {
                            DoorFactionLightData doorData = (DoorFactionLightData)item;
                            dobj["W " + doorData.x + " " + doorData.y] = doorData.GetAsBSON();
                        }*/
                    }
                    if (dobj.Keys.Count > 0)
                    {
                        byte[] dump = SimpleBSON.Dump(dobj);
                        ms.Write(BitConverter.GetBytes(dump.Length));
                        ms.Write(dump);
                    }
                    else
                    {
                        ms.Write(BitConverter.GetBytes((int)0));
                    }
                }
                else
                {
                    ms.Write(BitConverter.GetBytes((int)0));
                }
                File.WriteAllBytes(path, ms.ToArray());
                SpinWait.SpinUntil(() => Util.IsFileReady(path));
            }
        }



        public class ShuffleBag<T>
        {
            private List<T> items;
            private Random random;
            private int currentIndex;

            public ShuffleBag()
            {
                items = new List<T>();
                random = new Random();
                currentIndex = 0;
            }

            public void Add(T item, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    items.Add(item);
                }
                Shuffle();
            }

            public T Next()
            {
                if (currentIndex >= items.Count)
                {
                    Shuffle();
                    currentIndex = 0;
                }
                return items[currentIndex++];
            }

            private void Shuffle()
            {
                for (int i = items.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    T temp = items[i];
                    items[i] = items[j];
                    items[j] = temp;
                }
            }
        }

        public void SetupTerrain()
        {
            Util.Log("Setting up world terrain...");

            int bedrockRows = 3;  // Number of bedrock rows
            int bottomLayerHeight = 10;
            int middleLayerHeight = 16;
            int topLayerHeight = 1;

            tiles = new WorldTile[80, 60];

            // Initialize all tiles
            for (int i = 0; i < tiles.GetLength(1); i++)
            {
                for (int j = 0; j < tiles.GetLength(0); j++)
                {
                    tiles[j, i] = new WorldTile();
                }
            }

            // Create shuffle bags for random block distribution
            ShuffleBag<int> bottomLayerShuffleBag = new ShuffleBag<int>();
            ShuffleBag<int> middleLayerShuffleBag = new ShuffleBag<int>();
            ShuffleBag<int> topLayerShuffleBag = new ShuffleBag<int>();

            // Fill shuffle bags with block types and their counts
            {
                int obsidianCount = 25;
                int marbleCount = 7;
                int lavaCount = 9;
                int graniteCount = 11;
                int soilBlockCount = bottomLayerHeight * tiles.GetLength(0) - lavaCount - graniteCount - obsidianCount - marbleCount;

                bottomLayerShuffleBag.Add(1, soilBlockCount); // SoilBlock
                bottomLayerShuffleBag.Add(8, lavaCount);      // Marble
                bottomLayerShuffleBag.Add(7, lavaCount);      // Lava
                bottomLayerShuffleBag.Add(4, graniteCount);   // Granite
                bottomLayerShuffleBag.Add(2735, obsidianCount);  // Gem Soil
            }

            {
                int graniteCount = 35;
                int gemsoilCount = 25;
                int soilBlockCount = middleLayerHeight * tiles.GetLength(0) - graniteCount;

                middleLayerShuffleBag.Add(1, soilBlockCount); // SoilBlock
                middleLayerShuffleBag.Add(4, graniteCount);   // Granite
                middleLayerShuffleBag.Add(2735, gemsoilCount);   // Granite
            }

            {
                int soilBlockCount = topLayerHeight * tiles.GetLength(0);

                topLayerShuffleBag.Add(1, soilBlockCount);    // SoilBlock
            }

            // Define the height of the layers, reducing the height of the top layer
            int totalHeight = bedrockRows + bottomLayerHeight + middleLayerHeight + topLayerHeight;

            // Set up terrain layers below the spawn point
            for (int x = 0; x < tiles.GetLength(0); x++)
            {
                for (int y = 0; y < totalHeight; y++)
                {
                    if (y < bedrockRows)
                    {
                        tiles[x, y].fg.id = (short)3; // Bedrock
                    }
                    else if (y < bedrockRows + bottomLayerHeight)
                    {
                        tiles[x, y].fg.id = (short)bottomLayerShuffleBag.Next();
                    }
                    else if (y >= bedrockRows + bottomLayerHeight + middleLayerHeight)
                    {
                        tiles[x, y].fg.id = (short)topLayerShuffleBag.Next();
                    }
                    else
                    {
                        tiles[x, y].fg.id = (short)middleLayerShuffleBag.Next();
                    }

                    tiles[x, y].bg.id = (short)2; // Default background
                }
            }

            // Worlde rasgele bir�ey spawn edeceksen
        //    int entranceX = GetSizeX() / 2;
          //  int entranceY = totalHeight;

          //  tiles[entranceX, entranceY].fg.id = 110; // Assuming 100 is the ID for the entrance portal

            BackGroundType = LayerBackgroundType.ForestBackground;
            WeatherType = WeatherType.None;
        }


        public void SetupTerrainClear()
        {
            Util.Log("Setting up world terrain...");
            // empty world for now
            tiles = new WorldTile[80, 60];

            for (int i = 0; i < tiles.GetLength(1); i++)
            {
                for (int j = 0; j < tiles.GetLength(0); j++)
                {
                    tiles[j, i] = new WorldTile();
                }
            }

            for (int y = 0; y < SpawnPointY; y++)
            {
                for (int x = 0; x < GetSizeX(); x++)
                {
                    tiles[x, y].fg.id = 0;
                    tiles[x, y].bg.id = 0;
                }
            }

          
        }





        public WorldTile GetTile(int x, int y)
        {
            if (x >= GetSizeX() || y >= GetSizeY() || x < 0 || y < 0)
                return null;

            return tiles[x, y];
        }

        public BSONObject Serialize()
        {
            BSONObject wObj = new BSONObject();

            int tileLen = tiles.Length;
            int allocLen = tileLen * 2;

            byte[] blockLayerData = new byte[allocLen];
            byte[] backgroundLayerData = new byte[allocLen];
            byte[] waterLayerData = new byte[allocLen];
            byte[] wiringLayerData = new byte[allocLen];

            int width = GetSizeX();
            int height = GetSizeY();

            Util.Log($"Serializing world '{WorldName}' with width: {width} and height: {height}.");

            int pos = 0;
            for (int i = 0; i < tiles.Length; ++i)
            {
                int x = i % width;
                int y = i / width;

                if (x == SpawnPointX && y == SpawnPointY)
                    tiles[x, y].fg.id = 110;

                if (tiles[x, y].fg.id != 0) Buffer.BlockCopy(BitConverter.GetBytes(tiles[x, y].fg.id), 0, blockLayerData, pos, 2);
                if (tiles[x, y].bg.id != 0) Buffer.BlockCopy(BitConverter.GetBytes(tiles[x, y].bg.id), 0, backgroundLayerData, pos, 2);
                if (tiles[x, y].water.id != 0) Buffer.BlockCopy(BitConverter.GetBytes(tiles[x, y].water.id), 0, waterLayerData, pos, 2);
                if (tiles[x, y].wire.id != 0) Buffer.BlockCopy(BitConverter.GetBytes(tiles[x, y].wire.id), 0, wiringLayerData, pos, 2);
                pos += 2;
            }

            wObj[MsgLabels.MessageID] = MsgLabels.Ident.GetWorld;
            wObj["World"] = WorldName;
            wObj["BlockLayer"] = blockLayerData;
            wObj["BackgroundLayer"] = backgroundLayerData;
            wObj["WaterLayer"] = waterLayerData;
            wObj["WiringLayer"] = wiringLayerData;

            BSONObject cObj = new BSONObject();
            cObj["Count"] = collectables.Values.Count;

            for (int i = 0; i < collectables.Values.Count; i++)
            {
                var col = collectables.ElementAt(i).Value.GetAsBSON();
                var kv = collectables.ElementAt(i);

                col["CollectableID"] = kv.Key;
                cObj[$"C{i}"] = col;
            }
            BSONObject dobj = new BSONObject();
            foreach (WorldItemBase item in worldItems)
            {

                if (item.blockType == BlockType.LockWorld)
                {
                    var a = lockWorldData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.Door)
                {
                    DoorData doorData = (DoorData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.ScifiDoor)
                {
                    ScifiDoorData doorData = (ScifiDoorData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.BarnDoor)
                {
                    BarnDoorData doorData = (BarnDoorData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.GlassDoor)
                {
                    GlassDoorData doorData = (GlassDoorData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.CastleDoor)
                {
                    CastleDoorData doorData = (CastleDoorData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.GlassDoorTinted)
                {
                    GlassDoorTintedData doorData = (GlassDoorTintedData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.DungeonDoor || item.blockType == BlockType.DungeonDoorWhite)
                {
                    DungeonDoorData doorData = (DungeonDoorData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }/*
                else
                if (item.blockType == BlockType.DoorFactionDark)
                {
                    DoorFactionDarkData doorData = (DoorFactionDarkData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }
                else
                if (item.blockType == BlockType.DoorFactionLight)
                {
                    DoorFactionLightData doorData = (DoorFactionLightData)item;
                    var a = doorData.GetAsBSON();
                    dobj["W " + a["posX"].int32Value + " " + a["posY"].int32Value] = a;
                }*/
            }


            List<int>[] layerHits = new List<int>[4];
            for (int j = 0; j < layerHits.Length; j++)
            {
                layerHits[j] = new List<int>();
                layerHits[j].AddRange(Enumerable.Repeat(0, tileLen));
            }

            List<int>[] layerHitBuffers = new List<int>[4];
            for (int j = 0; j < layerHitBuffers.Length; j++)
            {
                layerHitBuffers[j] = new List<int>();
                layerHitBuffers[j].AddRange(Enumerable.Repeat(0, tileLen));
            }

            wObj["BlockLayerHits"] = layerHits[0];
            wObj["BackgroundLayerHits"] = layerHits[1];
            wObj["WaterLayerHits"] = layerHits[2];
            wObj["WiringLayerHits"] = layerHits[3];

            wObj["BlockLayerHitBuffers"] = layerHitBuffers[0];
            wObj["BackgroundLayerHitBuffers"] = layerHitBuffers[1];
            wObj["WaterLayerHitBuffers"] = layerHitBuffers[2];
            wObj["WiringLayerHits"] = layerHitBuffers[3];

            // change to template null count for optimization soon...
            BSONObject wLayoutType = new BSONObject();
            wLayoutType["Count"] = 0;
            BSONObject wBackgroundType = new BSONObject();
            wBackgroundType["Count"] = (int)BackGroundType;
            BSONObject wMusicSettings = new BSONObject();
            wMusicSettings["Count"] = 0;

            BSONObject wStartPoint = new BSONObject();
            wStartPoint["x"] = (int)SpawnPointX; wStartPoint["y"] = (int)SpawnPointY;

            BSONObject wSizeSettings = new BSONObject();
            wSizeSettings["WorldSizeX"] = width; wSizeSettings["WorldSizeY"] = height;
            BSONObject wGravityMode = new BSONObject();
            wGravityMode["Count"] = 0;
            BSONObject wRatings = new BSONObject();
            wRatings["Count"] = 0;
            BSONObject wRaceScores = new BSONObject();
            wRaceScores["Count"] = 0;
            BSONObject wLightingType = new BSONObject();
            wLightingType["Count"] = 0;
            BSONObject wWeatherType = new BSONObject();
            wWeatherType["Count"] = (int)WeatherType;


            wObj["WorldLayoutType"] = wLayoutType;
            wObj["WorldBackgroundType"] = wBackgroundType;
            wObj["WorldMusicIndex"] = wMusicSettings;
            wObj["WorldStartPoint"] = wStartPoint;
            wObj["WorldItemId"] = 0;
            wObj["WorldSizeSettings"] = wSizeSettings;
            //wObj["WorldGravityMode"] = wGravityMode;
            wObj["WorldRatingsKey"] = wRatings;
            wObj["WorldItemId"] = 1;
            wObj["InventoryId"] = 1;
            wObj["RatingBoardCountKey"] = 0;
            wObj["QuestStarterItemSummerCountKey"] = 0;
            wObj["WorldRaceScoresKey"] = wRaceScores;
            wObj["WorldTagKey"] = 0;
            wObj["PlayerMaxDeathsCountKey"] = 0;
            wObj["RatingBoardDateTimeKey"] = DateTimeOffset.UtcNow.Date;
            wObj["WorldLightingType"] = wLightingType;
            wObj["WorldWeatherType"] = wWeatherType;

            BSONObject pObj = new BSONObject();

            wObj["PlantedSeeds"] = pObj;
            wObj["Collectables"] = cObj;
            wObj["WorldItems"] = dobj;
            return wObj;
        }

        public void Deserialize(byte[] binary)
        {
            // load binary from file
         
            tiles = new WorldTile[80, 60]; // only this dimension is supported anyways
            for (int i = 0; i < tiles.GetLength(1); i++)
            {
                for (int j = 0; j < tiles.GetLength(0); j++)
                {
                    tiles[j, i] = new WorldTile();
                }
            }

            version = binary[0];

            int pos = 1;
            for (int y = 0; y < GetSizeY(); y++)
            {
                for (int x = 0; x < GetSizeX(); x++)
                {
                    var tile = tiles[x, y];

                    tile.fg.id = BitConverter.ToInt16(binary, pos);
                    tile.bg.id = BitConverter.ToInt16(binary, pos + 2);
                    tile.water.id = BitConverter.ToInt16(binary, pos + 4);
                    tile.wire.id = BitConverter.ToInt16(binary, pos + 6);

                    if (tile.fg.id == 110)
                    {
                        SpawnPointX = (short)x;
                        SpawnPointY = (short)y;
                    }

                    pos += 8;
                }
            }

            int dropCount = BitConverter.ToInt32(binary, pos); pos += 4;
            for (int i = 0; i < dropCount; i++)
            {
                Collectable c = new Collectable();
                c.item = BitConverter.ToInt16(binary, pos);
                c.amt = BitConverter.ToInt16(binary, pos + 2);
                c.posX = BitConverter.ToDouble(binary, pos + 4);
                c.posY = BitConverter.ToDouble(binary, pos + 12);
                c.gemType = BitConverter.ToInt16(binary, pos + 20);
                    c.type = BitConverter.ToInt16(binary, pos + 22);
                pos += 24;
                collectables[++colID] = c;
            }
            if (pos < binary.Length)
            {
                BackGroundType = (WorldInterface.LayerBackgroundType)BitConverter.ToInt32(binary, pos);
                WeatherType = (WorldInterface.WeatherType)BitConverter.ToInt32(binary, pos + 4);
                pos += 8;

            }
            int len = BitConverter.ToInt32(binary, pos);
            
            pos += 4;
            if (len > 0)
            {
                BSONObject dobj = SimpleBSON.Load(new ArraySegment<byte>(binary, pos, len).ToArray());
                foreach (string key in dobj.Keys)
                {
                    if (dobj[key][WorldItemBase.classKey].stringValue == "LockWorldData")
                    {
                        lockWorldData = new LockWorldData(dobj[key]["itemId"].int32Value);
                        lockWorldData.SetViaBSON(dobj[key] as BSONObject);
                        if(!worldItems.Contains(lockWorldData))worldItems.Add(lockWorldData);
                    }else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "DoorData")
                    {
                        DoorData doorData = new DoorData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }
                    else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "ScifiDoorData")
                    {
                        ScifiDoorData doorData = new ScifiDoorData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }
                    else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "CastleDoorData")
                    {
                        CastleDoorData doorData = new CastleDoorData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }
                    else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "BarnDoorData")
                    {
                        BarnDoorData doorData = new BarnDoorData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }
                    else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "GlassDoorData")
                    {
                        GlassDoorData doorData = new GlassDoorData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }
                    else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "GlassDoorTintedData")
                    {
                        GlassDoorTintedData doorData = new GlassDoorTintedData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }
                    else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "DungeonDoorData")
                    {
                        DungeonDoorData doorData = new DungeonDoorData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }
                    /*else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "DoorFactionLightData")
                    {
                        DoorFactionLightData doorData = new DoorFactionLightData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }else
                    if (dobj[key][WorldItemBase.classKey].stringValue == "DoorFactionDarkData")
                    {
                        DoorFactionDarkData doorData = new DoorFactionDarkData(dobj[key]["itemId"].int32Value);
                        doorData.SetViaBSON(dobj[key] as BSONObject);
                        worldItems.Add(doorData);
                    }*/
                }
            }   

        }
        public bool CanKick(Player p, string id)
        {
            WorldSession w = p.world;
            if (w == null) return false;
            if (p == null) return false;
            if (w.lockWorldData == null) return false;
            if (p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID))
                return true;

            return false;
        }
        public bool CanBan(Player p, string id)
        {
            WorldSession w = p.world;
            if (w == null) return false;
            if (p == null) return false;
            if (w.lockWorldData == null) return false;
            if (p.world.lockWorldData.DoesPlayerHaveAccessToLock(p.Data.UserID))
                return true;

            return false;
        }
        public bool CanSummon(Player p, string id)
        {
            WorldSession w = p.world;

            if (w == null) return false;
            if (p == null) return false;
            if (w.lockWorldData == null) return false;
            if (p.world.lockWorldData.DoesPlayerHaveMinorAccessToLock(p.Data.UserID))
                return true;

            return false;
        }

        public bool SetBlock(int x, int y, short blockType, Player p)
        {
            if (this.GetTile(x, y).fg.id != 0) return false;
            if (blockType == (short)BlockType.LockWorld)
            {
                foreach (WorldItemBase item in worldItems)
                {
                    if (item.blockType == BlockType.LockWorld)
                    {
                        return false;
                    }
                }
                this.itemIndex++;
                lockWorldData = new LockWorldData(itemIndex);
                lockWorldData.SetPlayerWhoOwnsLockId(p.Data.UserID);
                lockWorldData.SetPlayerWhoOwnsLockName(p.Data.Name);
                lockWorldData.SetLastActivatedTime(DateTime.UtcNow);
                lockWorldData.SetCreationTime(DateTime.UtcNow);
                lockWorldData.x = x; lockWorldData.y = y;
                worldItems.Add(lockWorldData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }else
            if (blockType == (short)BlockType.Door)
            {
                this.itemIndex++;
                DoorData doorData = new DoorData(itemIndex);
                doorData.SetIsLocked(true);
                doorData.x = x;
                doorData.y = y;
                worldItems.Add(doorData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }
            else
            if (blockType == (short)BlockType.ScifiDoor)
            {
                this.itemIndex++;
                ScifiDoorData doorData = new ScifiDoorData(itemIndex);
                doorData.SetIsLocked(true);
                doorData.x = x;
                doorData.y = y;
                worldItems.Add(doorData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }
            else
            if (blockType == (short)BlockType.BarnDoor)
            {
                this.itemIndex++;
                BarnDoorData doorData = new BarnDoorData(itemIndex);
                doorData.SetIsLocked(true);
                doorData.x = x;
                doorData.y = y;
                worldItems.Add(doorData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }
            else
            if (blockType == (short)BlockType.GlassDoor)
            {
                this.itemIndex++;
                GlassDoorData doorData = new GlassDoorData(itemIndex);
                doorData.SetIsLocked(true);
                doorData.x = x;
                doorData.y = y;
                worldItems.Add(doorData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }
            else
            if (blockType == (short)BlockType.GlassDoorTinted)
            {
                this.itemIndex++;
                GlassDoorTintedData doorData = new GlassDoorTintedData(itemIndex);
                doorData.SetIsLocked(true);
                doorData.x = x;
                doorData.y = y;
                worldItems.Add(doorData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }
            else
            if (blockType == (short)BlockType.DungeonDoor || blockType == (short)BlockType.DungeonDoorWhite)
            {
                this.itemIndex++;
                DungeonDoorData doorData = new DungeonDoorData(itemIndex);
                doorData.SetIsLocked(true);
                doorData.x = x;
                doorData.y = y;
                worldItems.Add(doorData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }
            /* else
             if (blockType == (short)BlockType.DoorFactionLight)
             {
                 this.itemIndex++;
                 DoorFactionLightData doorData = new DoorFactionLightData(itemIndex);
                 doorData.SetIsLocked(true);
                 doorData.x = x;
                 doorData.y = y;
                 worldItems.Add(doorData);
                 var t = this.GetTile(x, y);
                 t.fg.id = blockType;
                 t.fg.damage = 0;
                 t.fg.lastHit = 0;
                 return true;
             }
             else
             if (blockType == (short)BlockType.DoorFactionDark)
             {
                 this.itemIndex++;
                 DoorFactionDarkData doorData = new DoorFactionDarkData(itemIndex);
                 doorData.SetIsLocked(true);
                 doorData.x = x;
                 doorData.y = y;
                 worldItems.Add(doorData);
                 var t = this.GetTile(x, y);
                 t.fg.id = blockType;
                 t.fg.damage = 0;
                 t.fg.lastHit = 0;
                 return true;
             }*/
            else
            if (blockType == (short)BlockType.CastleDoor)
            {
                this.itemIndex++;
                CastleDoorData doorData = new CastleDoorData(itemIndex);
                doorData.SetIsLocked(true);
                doorData.x = x;
                doorData.y = y;
                worldItems.Add(doorData);
                var t = this.GetTile(x, y);
                t.fg.id = blockType;
                t.fg.damage = 0;
                t.fg.lastHit = 0;
                return true;
            }
            Item it = ItemDB.GetByID((int)blockType);
            switch (it.type)
            {
                case 3:
                    {
                        var t = this.GetTile(x, y);
                        t.water.id = blockType;
                        t.water.damage = 0;
                        t.water.lastHit = 0;
                        break;
                    }
                default:
                    {

                        var t = this.GetTile(x, y);
                        t.fg.id = blockType;
                        t.fg.damage = 0;
                        t.fg.lastHit = 0;

                        break;
                    }
            }
            return true;
        }
        public WorldItemBase FindItemBaseWithID(int id)
        {
            WorldItemBase worldItem = worldItems.Find(item => item.itemId == id);
            return worldItem;
        }

        public class Vector2i
        {
            public int x;
            public int y;

            public Vector2i(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }


        public class SeedData
        {
            private static readonly int m_GrowthTimeInSecondsForZeroOrLessComplexity = 8640000;
            private static readonly int m_MinGrowthTimeInSeconds = 30;
            private static readonly int m_MaxGrowthTimeInSeconds = 31536000;

            public int SeedID { get; set; }
            public int ItemID { get; set; }
            public DateTime GrowthEndTime { get; set; }
            public int GrowthDurationInSeconds { get; set; }
            public bool IsAlreadyCrossBred { get; set; }

            public Vector2i Position { get; set; }
            public int PositionX { get; set; }
            public int PositionY { get; set; }
            public short HarvestSeeds { get; set; }
            public short HarvestBlocks { get; set; }
            public short HarvestGems { get; set; }
            public short HarvestExtraBlocks { get; set; }

            public SeedData(int seedID, int itemID, int posX, int posY, int growthDurationSeconds, bool isMixed = false)
            {
                SeedID = seedID;
                ItemID = itemID;
                PositionX = posX;
                PositionY = posY;
                GrowthEndTime = DateTime.UtcNow.AddSeconds(growthDurationSeconds);
                GrowthDurationInSeconds = growthDurationSeconds;
                IsAlreadyCrossBred = isMixed;
                HarvestSeeds = (short)(RollDrops.DoesTreeDropSeed(itemID) ? 1 : 0);
                HarvestBlocks = RollDrops.TreeDropsBlocks(itemID);
                HarvestGems = RollDrops.TreeDropsGems(itemID);
                HarvestExtraBlocks = (short)(RollDrops.DoesTreeDropExtraBlock(itemID) ? 1 : 0);
            }

            public static int CalculateGrowthTimeInSeconds(int blockComplexity)
            {
                if (blockComplexity <= 0)
                {
                    return m_GrowthTimeInSecondsForZeroOrLessComplexity;
                }

                double growthTime = Math.Floor(Math.Pow(blockComplexity, 3.2) + 30.0 * Math.Pow(blockComplexity, 1.4));

                if (growthTime < m_MinGrowthTimeInSeconds)
                {
                    growthTime = m_MinGrowthTimeInSeconds;
                }
                else if (growthTime > m_MaxGrowthTimeInSeconds)
                {
                    growthTime = m_MaxGrowthTimeInSeconds;
                }

                return (int)growthTime;
            }
        }

        public static class RollDrops
        {
            public static bool DoesTreeDropSeed(int itemID)
            {
                // Implement logic to determine if a tree drops seeds
                return true;
            }

            public static short TreeDropsBlocks(int itemID)
            {
                // Implement logic to determine how many blocks a tree drops
                return 1;
            }

            public static short TreeDropsGems(int itemID)
            {
                // Implement logic to determine how many gems a tree drops
                return 1;
            }

            public static bool DoesTreeDropExtraBlock(int itemID)
            {
                // Implement logic to determine if a tree drops extra blocks
                return false;
            }


        }


        public bool IsPlayerBanned(Player p)
        {
            if(banList.ContainsKey(p.Data.UserID))
            {
                if (banList[p.Data.UserID] > DateTime.UtcNow.Ticks)
                {
                    return true;
                }
            }
            banList.Remove(p.Data.UserID);
            return false;
        }



 

        



        ~WorldSession()
        {

        }
    }
}
