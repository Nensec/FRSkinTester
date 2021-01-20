using Discord.Commands;
using FRTools.Common;
using System;
using System.Threading.Tasks;

namespace Discord
{
    public static class DiscordExtentions
    {
        public static async Task DelayedDelete(this IMessage message, TimeSpan delay)
        {
            await Task.Delay(delay);
            await message.DeleteAsync();
        }

        public static LoggingContext AsLoggingContext(this ICommandContext commandContext) =>
            new LoggingContext(null, (long)commandContext.User.Id, (long)commandContext.Channel.Id, (long?)commandContext.Guild?.Id);
    }
}
