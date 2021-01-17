using Discord.Commands;
using Discord.WebSocket;
using FRTools.Common;

namespace FRTools.Discord.Infrastructure
{
    public class FRToolsCommandContext : SocketCommandContext
    {
        public FRToolsLogger Logger { get; }

        public FRToolsCommandContext(DiscordSocketClient client, SocketUserMessage msg, FRToolsLogger logger) : base(client, msg)
        {
            Logger = logger;
        }
    }
}
