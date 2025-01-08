using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PixelWorldsServer2
{
    public class DiscordBot
    {
        private static DiscordSocketClient _client = new DiscordSocketClient();
        private static CommandService _commands = new CommandService();
        private static IServiceProvider _services;
        private const string token = "MTIxMzE4OTQ0MzAyNDg1MTAxNA.GJ11WG.v7BMc0g8yeQ4sdUIwKA5xdSMx52ATEH-OP8gPE";



        public static bool hasInit = false;

        public static async Task UpdateStatus(string status)
        {
            try
            {
                await _client.SetGameAsync(status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating status: {ex.Message}");
            }
        }




        private static Task InternalLog(LogMessage msg)
        {
            // You can add logging functionality here if needed
            return Task.CompletedTask;
        }

        private static Task Connected()
        {
            Util.Log("Discord Bot connected successfully!");
            return Task.CompletedTask;
        }

        public static void Init()
        {
            _client.Connected += Connected;
            _client.Log += InternalLog;
            _client.Ready += async () =>
            {
                // Call UpdateStatus when the bot is ready
                await UpdateStatus("LTPS is UP! Join the server now.");

                // Register commands
                await RegisterCommandsAsync();

                hasInit = true;
            };

            _ = Login();
        }

        public static async Task Login()
        {
            try
            {
                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
            }
        }

        private static async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private static async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);

            if (message.Author.IsBot) return;

            int argPos = 0;
            if (message.HasStringPrefix("!", ref argPos))
            {
                var result = await _commands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess) Console.WriteLine(result.ErrorReason);
            }
        }
    }

    public class BasicCommands : ModuleBase<SocketCommandContext>
    {
        [Command("help")]
        public async Task HelpCommand()
        {
            string response = @"```markdown
🎉 www.LTPS.pw 🎉

#1 Pixel Worlds Private Server

You can convert LTPS Bytecoins to Real PW Bytecoins easily.

All Items easy to obtain & Join our server and have fun! 🎉

Type !howtoplay to see the tutorial.

Commands:

!help , !howtoplay , !status , !convert , !price
```";
            await ReplyAsync(response);
        }

        [Command("price")]
        public async Task PriceCommand()
        {
            string response = @"```markdown
💰 LTPS Bytecoins Rate to Exchange 💰

Date: 30/06/2024
Price: 1 LTPS Bytecoin / 30 Real PW Bytecoins (till 02.07.2024)

If you want to convert please use !convert command on LTPS server and send a request.
Your Exchange request changes with the exchange rate on which day you send the request. 🎉
```";
            await ReplyAsync(response);
        }


        [Command("howtoplay")]
        [Summary("Check if the conversion service is currently closed.")]
        public async Task HowtoPlayCommand()
        {
            string response = "📋 Tutorial: https://ltps.pw/pc";
            await ReplyAsync(response);
        }



        [Command("convert")]
        [Summary("Check if the conversion service is currently closed.")]
        public async Task ConvertCommand()
        {
            string response = "💰 Exchange system is open. Please use #convert channel on LTPS to convert your bytecoins.";
            await ReplyAsync(response);
        }


        [Command("status")]
        [Summary("Check server status.")]
        public async Task StatusCommand()
        {
            string response = "🟢 Server is online!\n🔴 Chance of the server shutting down at any time: %1.3";
            await ReplyAsync(response);
        }






    }

}
