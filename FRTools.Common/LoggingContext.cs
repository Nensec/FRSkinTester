using FRTools.Data.DataModels;

namespace FRTools.Common
{
    public class LoggingContext
    {
        public User User { get; set; }
        public long? DiscordUser { get; set; }
        public long? DiscordChannel { get; set; }
        public long? DiscordServer { get; set; }

        public LoggingContext(User user = null, long? discordUser = null, long? discordChannel = null, long? discordServer = null)
        {
            User = user;
            DiscordUser = discordUser;
            DiscordChannel = discordChannel;
            DiscordServer = discordServer;
        }
    }
}
