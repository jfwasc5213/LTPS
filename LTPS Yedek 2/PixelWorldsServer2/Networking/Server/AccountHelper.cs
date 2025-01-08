using System;
using System.Collections.Generic;
using System.Data;
using System.Numerics;
using System.Text;
using static PixelWorldsServer2.Player;

namespace PixelWorldsServer2.Networking.Server
{
    public class AccountHelper
    {
        PWServer pServer = null;
        public AccountHelper(PWServer pServer)
        {
            this.pServer = pServer;
        }

        public Player LoginPlayer(string ip, bool forceRegister = true)
        {
            Player player = null;
            var sql = pServer.GetSQL();

            var cmd = sql.Make("SELECT * FROM players WHERE IP=@IP");

            cmd.Parameters.AddWithValue("@IP", ip);

            using (var reader = sql.PreparedFetchQuery(cmd))
            {
                if (reader != null)
                {
                    if (reader.Read())
                    {
                        player = new Player(reader);
                        // Set the player's nickname
                        player.Data.Name = reader.GetString("Name");
                    }
                    else
                    {
                        if (forceRegister)
                        {
                            player = CreateAccount(ip);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Error reading player data from the database.");
                }
            }

            return player;
        }


        public Player CreateAccount(string ip = "", int adminStatus = 0)
        {
            var sql2 = pServer.GetSQL();
            bool validID = false;
            int count = 20;
            string ID = "";
            while (count > 0 && !validID)
            {
                try
                {
                    var cmdt = sql2.Make("SELECT * FROM players WHERE ID=@ID");
                    ID = Util.RandomString(8);
                    cmdt.Parameters.AddWithValue("@ID", ID);

                    using (var reader = sql2.PreparedFetchQuery(cmdt))
                    {
                        if (reader != null)
                        {
                            if (reader.Read())
                            { count--; continue; }
                            else
                            {
                                validID = true;
                                continue;
                            }
                        }
                        else
                        {
                            validID = true;
                            continue;
                        }
                    }
                }
                catch { count--; }
            }
            if (count == 0) return null;

            var sql = pServer.GetSQL();

            var cmd = sql.Make("INSERT INTO players (ID, Name, IP, AdminStatus, RecentWorlds) VALUES (@ID, @Name, @IP, @AdminStatus, @RecentWorlds)");

            cmd.Parameters.AddWithValue("@ID", ID);
            string name = "LTPS_" + Util.RandomString(8);
    
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@IP", ip);
            cmd.Parameters.AddWithValue("@AdminStatus", adminStatus);
            cmd.Parameters.AddWithValue("@RecentWorlds", "");

            int rowsAffected = sql.PreparedQuery(cmd);
            if (rowsAffected > 0)
            {
                Player player = new Player();
                return player;
            }
            else
            {
                return null;
            }
        }

        public bool UpdatePlayer(int playerId, string name = null, string ip = null, int? adminStatus = null)
        {
            var sql = pServer.GetSQL();

            try
            {
                var cmd = sql.Make("UPDATE players SET " +
                                   (name != null ? "Name = @Name, " : "") +
                                   (ip != null ? "IP = @IP, " : "") +
                                   (adminStatus.HasValue ? "AdminStatus = @AdminStatus, " : "") +
                                   "WHERE ID = @PlayerID");

                if (name != null)
                    cmd.Parameters.AddWithValue("@Name", name);

                if (ip != null)
                    cmd.Parameters.AddWithValue("@IP", ip);

                if (adminStatus.HasValue)
                    cmd.Parameters.AddWithValue("@AdminStatus", adminStatus.Value);

                cmd.Parameters.AddWithValue("@PlayerID", playerId);

                int rowsAffected = sql.PreparedQuery(cmd);

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while updating the player: " + ex.Message);
                return false;
            }
        }
    }
}
