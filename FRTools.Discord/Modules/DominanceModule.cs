using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using FRTools.Common;
using FRTools.Data;
using FRTools.Discord.Handlers;
using FRTools.Discord.Infrastructure;

namespace FRTools.Discord.Modules
{
    [Name("Dominance"), Group("dominance"), Alias("dom"), Summary("Dominance related commands")]
    [DiscordSetting("GUILDCONFIG_DOMINANCE", typeof(bool), "Automatic dominance", "Enables or disables automatically assigning the dominance role to the current week's dominance winner. This can also be turned on and off with a discord bot command.", "Enabled", "Disabled")]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ANNOUNCE", typeof(bool), "Dominance announcements", "Enables or disables the announcement of the current week's standings when the bot fetches the update. Requires $<GUILD:GUILDCONFIG_ANN_CHANNEL> to be set.", "Enabled", "Disabled")]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE", typeof(IRole), "Dominance role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_0", typeof(IRole), "Earth role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_1", typeof(IRole), "Plague role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_2", typeof(IRole), "Wind role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_3", typeof(IRole), "Water role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_4", typeof(IRole), "Lightning role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_5", typeof(IRole), "Ice role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_6", typeof(IRole), "Shadow role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_7", typeof(IRole), "Light role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_8", typeof(IRole), "Arcane role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_9", typeof(IRole), "Nature role", null)]
    [DiscordSetting("GUILDCONFIG_DOMINANCE_ROLE_10", typeof(IRole), "Fire role", null)]
    [DiscordHelp("DominanceModule")]
    public class DominanceModule : BaseModule
    {
        private readonly IServiceProvider _serviceProvider;

        public DominanceModule(DataContext dbContext, SettingManager settingManager, IServiceProvider serviceProvider, FRToolsLogger logger) : base(dbContext, settingManager, logger)
        {
            _serviceProvider = serviceProvider;
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Name("Setup"), Command("setup", RunMode = RunMode.Async), Summary("Setup auto dominance role")]
        [DiscordHelp("DominanceSetup", GuildPermission.Administrator)]
        public async Task Setup()
        {
            var roleTypeReader = new RoleTypeReader<IRole>();

            IRole domRole = null;
            IRole[] flightRoles = new IRole[11];

            var embedBuilder = new EmbedBuilder()
                .WithTitle("Auto-dominance setup")
                .WithDescription("Welcome to the setup for auto-dominance role!\r\nThis small setup will guide you in setting up all the needed information for me to automagically assign your dominance role to this week's domninance winners.\r\nEvery question has a 30 second time limit to respond, so make sure you have all roles ready.\r\n\nYou can always reply `Stop` to stop the setup.\r\n\nTo begin, what is the role for **Dominance**?");

            var interactiveMessage = await ReplyAsync(embed: embedBuilder.Build());

            Task StopSetup()
            {
                embedBuilder.Fields.RemoveAll(x => true);
                embedBuilder.Description = $"Setup has been cancelled.";
                return interactiveMessage.ModifyAsync(x => x.Embed = embedBuilder.Build());
            }

            while (true)
            {
                var roleMessage = await NextMessageAsync(true, true, TimeSpan.FromSeconds(30));
                if (roleMessage == null || roleMessage.Content.ToLower().Trim() == "stop")
                {
                    await StopSetup();
                    return;
                }

                var roleResult = await roleTypeReader.ReadAsync(Context, roleMessage.Content, _serviceProvider);
                if (roleResult.IsSuccess)
                {
                    if (Context.Guild.CurrentUser.GuildPermissions.ManageMessages)
                        await roleMessage.DeleteAsync();
                    domRole = (IRole)roleResult.BestMatch;
                    break;
                }
                else
                {
                    embedBuilder.Description = $"I could not find a role with the following input: `{roleMessage.Content}`.\r\n\nPlease try again, you can provide a mention (`@role`) name (`role`) or the id of the role (`000000000000000000`).\r\nWhat is the role for **Dominance**?";
                    if (Context.Guild.CurrentUser.GuildPermissions.ManageMessages)
                        await roleMessage.DeleteAsync();

                    await interactiveMessage.ModifyAsync(x => x.Embed = embedBuilder.Build());
                }
            }

            embedBuilder.AddField("Dominance", domRole.Mention);
            embedBuilder.Description = $"Great! I've found the role {domRole.Mention} and will set that as the role used for **Dominance** once this setup is finished.\r\n\nNext up are the flight roles. What is the role for **{Flight.Earth}**?";
            await interactiveMessage.ModifyAsync(x => x.Embed = embedBuilder.Build());

            for (var i = 0; i <= 10; i++)
            {
                if (flightRoles.Any(x => x != null))
                {
                    embedBuilder.Description = $"Great! I've found the role {flightRoles[i - 1].Mention} and will set that as the role used for **{(Flight)i}** once this setup is finished.\r\n\nNext one! What is the role for **{(Flight)i}**?";
                    await interactiveMessage.ModifyAsync(x => x.Embed = embedBuilder.Build());
                }
                while (true)
                {
                    var roleMessage = await NextMessageAsync(true, true, TimeSpan.FromSeconds(30));
                    if (roleMessage == null || roleMessage.Content.ToLower().Trim() == "stop")
                    {
                        await StopSetup();
                        return;
                    }

                    var roleResult = await roleTypeReader.ReadAsync(Context, roleMessage.Content, _serviceProvider);
                    if (roleResult.IsSuccess)
                    {
                        if (Context.Guild.CurrentUser.GuildPermissions.ManageMessages)
                            await roleMessage.DeleteAsync();
                        flightRoles[i] = (IRole)roleResult.BestMatch;

                        if (embedBuilder.Fields.Any(x => x.Name == "Flights"))
                            embedBuilder.Fields[1].Value = string.Join("\n", flightRoles.Where(x => x != null).Select(x => x.Mention));
                        else
                            embedBuilder.AddField("Flights", string.Join("\n", flightRoles.Where(x => x != null).Select(x => x.Mention)));

                        break;
                    }
                    else
                    {
                        embedBuilder.Description = $"I could not find a role with the following input: `{roleMessage.Content}`.\r\n\nPlease try again, you can provide a mention (`@role`) name (`role`) or the id of the role (`000000000000000000`).\r\nWhat is the role for **{(Flight)i}**?";
                        if (Context.Guild.CurrentUser.GuildPermissions.ManageMessages)
                            await roleMessage.DeleteAsync();

                        await interactiveMessage.ModifyAsync(x => x.Embed = embedBuilder.Build());
                    }
                }
            }

            if (domRole != null && flightRoles.All(x => x != null))
            {
                embedBuilder.Description = $"Great! I've found the role {flightRoles[10].Mention} and will set that as the role used for **{(Flight.Fire)}**.\r\n\nThat was the end of the setup, everything is now saved and when dominance rolls over on Flight Rising I will update your users' roles!\r\n\nMake sure to have people opt in to this using `{SettingManager.GetSettingValue("GUILDCONFIG_PREFIX", Context.Guild)}dom optin`!";
                await interactiveMessage.ModifyAsync(x => x.Embed = embedBuilder.Build());

                SettingManager.SetSettingValue("GUILDCONFIG_DOMINANCE", "true", Context.Guild);

                SettingManager.SetSettingValue("GUILDCONFIG_DOMINANCE_ROLE", domRole.Id.ToString(), Context.Guild);
                for (var i = 0; i < 11; i++)
                    SettingManager.SetSettingValue($"GUILDCONFIG_DOMINANCE_ROLE_{i}", flightRoles[i].Id.ToString(), Context.Guild);
            }
            else
            {
                await StopSetup();
                return;
            }
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Name("Enable"), Command("enable"), Summary("Enables the automatic dominance role")]
        [DiscordHelp("DominanceEnable", GuildPermission.Administrator)]
        public async Task Enable()
        {
            if (SettingManager.GetSettingValue("GUILDCONFIG_DOMINANCE_ROLE", Context.Guild) != null && Enumerable.Range(0, 10).All(x => SettingManager.GetSettingValue($"GUILDCONFIG_DOMINANCE_ROLE_{x}", Context.Guild) != null))
            {
                SettingManager.SetSettingValue("GUILDCONFIG_DOMINANCE", "true", Context.Guild);
                await ReplyAsync(embed: new EmbedBuilder().WithDescription("Automatic dominance role has been enabled.").Build());
            }
            else
                await ReplyAsync(embed: new EmbedBuilder().WithDescription($"I do not have the required data to automatically assign the dominance role, please run `{SettingManager.GetSettingValue("GUILDCONFIG_PREFIX", Context.Guild)}dom setup` first.").Build());
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Name("Disable"), Command("disable"), Summary("Disables the automatic dominance role")]
        [DiscordHelp("DominanceDisable", GuildPermission.Administrator)]
        public async Task Disable()
        {
            SettingManager.SetSettingValue("GUILDCONFIG_DOMINANCE", "false", Context.Guild);
            await ReplyAsync(embed: new EmbedBuilder().WithDescription( "Automatic dominance role has been disabled.").Build());
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [Name("Update"), Command("update", RunMode = RunMode.Async), Summary("Manually trigger an update for this server")]
        [DiscordHelp("DominanceUpdate", GuildPermission.Administrator)]
        public async Task Update()
        {
            if (SettingManager.GetSettingValue("GUILDCONFIG_DOMINANCE", Context.Guild) == "true")
            {
                var embed = new EmbedBuilder().WithDescription("Running a manual update for this guild to assign dominance roles, this might take a bit..");
                var msg = await ReplyAsync(embed: embed.Build());
                await DominanceHandler.UpdateGuild(SettingManager, Context.Guild, Context.Guild.GetUser(Context.Client.CurrentUser.Id));
                embed.Description = "Manual update of dominance roles completed.";
                await msg.ModifyAsync(x => x.Embed = embed.Build());
            }
            else
                await ReplyAsync(embed: new EmbedBuilder().WithDescription($"Enable automatic dominance role first using `{SettingManager.GetSettingValue("GUILDCONFIG_PREFIX", Context.Guild)}dom enable`.").Build());
        }

        [Name("Optin"), Command("join"), Alias("iam", "optin"), Summary("Opt in to receive the dominance role when the flight you are part of wins dominance")]
        [DiscordHelp("DominanceOptIn")]
        public async Task OptIn()
        {
            if (SettingManager.GetSettingValue("GUILDCONFIG_DOMINANCE_ROLE", Context.Guild) == null || Enumerable.Range(0, 10).Any(x => SettingManager.GetSettingValue($"GUILDCONFIG_DOMINANCE_ROLE_{x}", Context.Guild) == null))
            {
                await ReplyAsync(embed: new EmbedBuilder().WithDescription($"This server does not have a dominance role set that I know of. Have an administrator run `{SettingManager.GetSettingValue("GUILDCONFIG_PREFIX", Context.Guild)}dom setup` first.").Build());
                return;
            }

            if (SettingManager.GetSettingValue($"GUILDCONFIG_DOMINANCE_USER_{Context.User.Id}", Context.Guild) == "true")
            {
                await ReplyAsync(embed: new EmbedBuilder().WithDescription("You are already signed up to receive the dominance role on this server.").Build());
                return;
            }

            SettingManager.SetSettingValue($"GUILDCONFIG_DOMINANCE_USER_{Context.User.Id}", "true", Context.Guild);
            await ReplyAsync(embed: new EmbedBuilder().WithDescription("You are now signed up to receive the dominance role on this server.").Build());
            using (var ctx = new DataContext())
            {
                var lastDominance = ctx.FRDominances.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                if (lastDominance != null)
                {
                    var firstPlaceRole = Context.Guild.GetRole(ulong.Parse(SettingManager.GetSettingValue($"GUILDCONFIG_DOMINANCE_ROLE_{lastDominance.First}", Context.Guild)));
                    if ((Context.User as IGuildUser).RoleIds.Contains(firstPlaceRole.Id))
                        await (Context.User as IGuildUser).AddRoleAsync(Context.Guild.GetRole(ulong.Parse(SettingManager.GetSettingValue("GUILDCONFIG_DOMINANCE_ROLE", Context.Guild))));
                }
            }
        }

        [Name("Optout"), Command("leave"), Alias("iamnot", "optout"), Summary("Opt out of receiving the dominance role")]
        [DiscordHelp("DominanceOptOut")]
        public async Task OptOut()
        {
            var dominanceRole = SettingManager.GetSettingValue("GUILDCONFIG_DOMINANCE_ROLE", Context.Guild);

            if (dominanceRole == null)
            {
                await ReplyAsync(embed: new EmbedBuilder().WithDescription($"This server does not have a dominance role set that I know of. Have an administrator run `{SettingManager.GetSettingValue("GUILDCONFIG_PREFIX", Context.Guild)}dom setup` first.").Build());
                return;
            }

            if (SettingManager.GetSettingValue($"GUILDCONFIG_DOMINANCE_USER_{Context.User.Id}", Context.Guild) == "false")
            {
                await ReplyAsync(embed: new EmbedBuilder().WithDescription("You are already set to not receive the dominance role.").Build());
                return;
            }
            SettingManager.SetSettingValue($"GUILDCONFIG_DOMINANCE_USER_{Context.User.Id}", "false", Context.Guild);
            await ReplyAsync(embed: new EmbedBuilder().WithDescription("You will now no longer receive the dominance role.").Build());
            await (Context.User as IGuildUser).RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(dominanceRole)));
        }

        [Command("manage")]
        public override Task ManageModule() => base.ManageModule();
    }
}
