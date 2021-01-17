using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using FRTools.Data;
using FRTools.Data.DataModels.DiscordModels;
using FRTools.Discord.Infrastructure;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Color = Discord.Color;

namespace FRTools.Discord.Modules
{
    public class BaseModule : InteractiveBase<FRToolsCommandContext>
    {
        protected DataContext DbContext { get; }
        protected SettingManager SettingManager { get; }
        protected DiscordServer Server { get; private set; }
        protected DiscordChannel Channel { get; private set; }

        protected string CDNBasePath = ConfigurationManager.AppSettings["CDNBasePath"];
        protected string WebsiteBaseUrl = ConfigurationManager.AppSettings["WebsiteBaseUrl"];

        private string _moduleName;

        protected string ModuleName
        {
            get
            {
                if (_moduleName != null)
                    return _moduleName;

                if (GetType().GetCustomAttributes(typeof(GroupAttribute), true).FirstOrDefault() is GroupAttribute groupAttr)
                    return _moduleName = groupAttr.Prefix;
                if (GetType().GetCustomAttributes(typeof(NameAttribute), true).FirstOrDefault() is NameAttribute nameAttr)
                    return _moduleName = nameAttr.Text;

                return _moduleName = GetType().Name.Replace("Module", "").ToLower();
            }
        }

        public BaseModule(DataContext dbContext, SettingManager settingManager)
        {
            DbContext = dbContext;
            SettingManager = settingManager;
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            if (Context.Guild != null)
            {
                Server = DbContext.DiscordServers.FirstOrDefault(x => x.ServerId == (long)Context.Guild.Id) ?? DbContext.DiscordServers.Add(new DiscordServer { ServerId = (long)Context.Guild.Id, Name = Context.Guild.Name });
                Channel = DbContext.DiscordChannels.FirstOrDefault(x => x.ChannelId == (long)Context.Channel.Id) ?? DbContext.DiscordChannels.Add(new DiscordChannel { Server = Server, ChannelId = (long)Context.Channel.Id, Name = Context.Channel.Name });
                DbContext.SaveChanges();
            }

            Context.Logger.Log(Data.DataModels.LogItemOrigin.Discord, Data.DataModels.LogItemSeverity.Info, $"Begin command execute: {command.Module.Name}.{command.Name}", discordUser: (long)Context.User.Id, discordChannel: (long)Context.Channel.Id, discordServer: (long?)Context.Guild?.Id);
            base.BeforeExecute(command);
        }

        protected override void AfterExecute(CommandInfo command)
        {
            if (!command.Attributes.Any(x => x is NoLogAttribute) && !command.Module.Attributes.Any(x => x is NoLogAttribute))
            {
                var discordLog = DbContext.DiscordLogs.Add(new DiscordLog { Channel = Channel, UserId = (long)Context.User.Id, Module = command.Module.Name, Command = command.Name, Data = Context.Message.Content });
                DbContext.SaveChanges();

                Context.Logger.Log(Data.DataModels.LogItemOrigin.Discord, Data.DataModels.LogItemSeverity.Info, $"End command execute: {command.Module.Name}.{command.Name} (DiscordLog Id: {discordLog.Id})", discordUser: (long)Context.User.Id, discordChannel: (long)Context.Channel.Id, discordServer: (long?)Context.Guild?.Id);
            }
            else
                Context.Logger.Log(Data.DataModels.LogItemOrigin.Discord, Data.DataModels.LogItemSeverity.Info, $"End command execute: {command.Module.Name}.{command.Name}", discordUser: (long)Context.User.Id, discordChannel: (long)Context.Channel.Id, discordServer: (long?)Context.Guild?.Id);

            base.AfterExecute(command);
        }

        public virtual Task ManageModule() => ReplyAsync(embed: new EmbedBuilder().WithDescription($"Visit the following page to manage this module: {ConfigurationManager.AppSettings["WebsiteBaseURL"]}/discord/manage/{Context.Guild.Id}/{ModuleName}").Build());

        [Command("help")]
        public Task Help() => ReplyAsync(embed: new EmbedBuilder().WithDescription($"Please see the following link for help. [click here]({ConfigurationManager.AppSettings["WebsiteBaseURL"]}/discord/help)").Build());

        protected EmbedBuilder ErrorEmbed(string errorMessage) => new EmbedBuilder().WithColor(Color.Red).WithDescription(errorMessage);
    }
}
