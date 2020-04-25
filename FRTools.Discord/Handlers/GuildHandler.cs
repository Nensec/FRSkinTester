﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using FRTools.Data;
using FRTools.Data.DataModels.DiscordModels;
using FRTools.Data.Messages;
using FRTools.Discord.Infrastructure;
using System.Data.Entity;
using System.Net;

namespace FRTools.Discord.Handlers
{
    [DiscordSetting("GUILDCONFIG_PREFIX", typeof(string), "Command prefix", "The prefix used by the bot to listen to commands")]
    [DiscordSetting("GUILDCONFIG_ANN_CHANNEL", typeof(ITextChannel), "Announcement channel", "The channel the bot will post announcement messages")]
    public class GuildHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _globalCommandService;
        private readonly SettingManager _settingManager;
        private readonly IServiceProvider _serviceProvider;

        public SocketGuild Guild { get; }

        public GuildHandler(SocketGuild guild, DiscordSocketClient client, CommandService globalCommandService, SettingManager settingManager, IServiceProvider serviceProvider)
        {
            Guild = guild;
            _client = client;
            _globalCommandService = globalCommandService;
            _settingManager = settingManager;
            _serviceProvider = serviceProvider;

            settingManager.GetAllGuildSettings(guild);
        }

        internal async Task HandleRoleCreated(SocketRole role)
        {
            using (var ctx = new DataContext())
            {
                ctx.DiscordServers.SingleOrDefault(x => x.ServerId == (long)role.Guild.Id)?.Roles.Add(new DiscordRole
                {
                    RoleId = (long)role.Id,
                    Name = role.Name,
                    DiscordPermissions = (long)role.Permissions.RawValue,
                    Color = role.Color.RawValue.ToString()
                });
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleRoleDeleted(SocketRole role)
        {
            using (var ctx = new DataContext())
            {
                var dbRole = ctx.DiscordRoles.SingleOrDefault(x => x.RoleId == (long)role.Id);
                ctx.DiscordRoles.Remove(dbRole);
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleRoleUpdate(SocketRole roleOld, SocketRole roleNew)
        {
            using (var ctx = new DataContext())
            {
                var dbRole = ctx.DiscordRoles.SingleOrDefault(x => x.RoleId == (long)roleNew.Id);
                if (dbRole != null)
                {
                    dbRole.Name = roleNew.Name;
                    dbRole.Color = roleNew.Color.RawValue.ToString();
                    dbRole.DiscordPermissions = (long)roleNew.Permissions.RawValue;
                    await ctx.SaveChangesAsync();
                }
            }
        }

        internal async Task HandleMemberUpdate(SocketGuildUser userOld, SocketGuildUser userNew)
        {
            using (var ctx = new DataContext())
            {
                var dbServer = ctx.DiscordServers.SingleOrDefault(x => x.ServerId == (long)userNew.Guild.Id);
                var dbServerUser = dbServer.Users.FirstOrDefault(x => x.User.UserId == (long)userNew.Id);
                if (dbServerUser == null)
                {
                    var dbUser = ctx.DiscordUsers.FirstOrDefault(x => x.UserId == (long)userNew.Id);
                    if (dbUser == null)
                        ctx.DiscordUsers.Add(dbUser = new DiscordUser { UserId = (long)userNew.Id });
                    dbServer.Users.Add(dbServerUser = new DiscordServerUser { User = dbUser });
                }
                dbServerUser.Nickname = userNew.Nickname;
                dbServerUser.Roles = dbServer.Roles.Where(x => userNew.Roles.Any(r => (long)r.Id == x.RoleId)).ToList();
                dbServerUser.User.Username = userNew.Username;
                dbServerUser.User.Discriminator = userNew.DiscriminatorValue;
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleUpdateGuild(SocketGuild guildOld, SocketGuild guildNew)
        {
            using (var ctx = new DataContext())
            {
                var guild = ctx.DiscordServers.SingleOrDefault(x => x.ServerId == (long)guildNew.Id);
                guild.Name = guildNew.Name;
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleChannelCreated(SocketGuildChannel guildChannel)
        {
            using (var ctx = new DataContext())
            {
                ctx.DiscordServers.SingleOrDefault(x => x.ServerId == (long)guildChannel.Guild.Id).Channels.Add(new DiscordChannel
                {
                    ChannelId = (long)guildChannel.Id,
                    Name = guildChannel.Name
                });
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleChannelRemoved(SocketGuildChannel guildChannel)
        {
            using (var ctx = new DataContext())
            {
                var channel = ctx.DiscordChannels.SingleOrDefault(x => x.ChannelId == (long)guildChannel.Id);
                ctx.DiscordChannels.Remove(channel);
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleChannelUpdated(SocketGuildChannel guildChannelOld, SocketGuildChannel guildChannelNew)
        {
            using (var ctx = new DataContext())
            {
                var channel = ctx.DiscordChannels.SingleOrDefault(x => x.ChannelId == (long)guildChannelNew.Id);
                if (channel != null)
                {
                    channel.Name = guildChannelNew.Name;
                    ctx.SaveChanges();
                }
                else
                    await HandleChannelCreated(guildChannelNew);
            }
        }

        internal async Task HandleMessage(SocketMessage msg)
        {
            int argPos = 0;
            var prefix = _settingManager.GetSettingValue("GUILDCONFIG_PREFIX", Guild) ?? "$";

#if DEBUG
            prefix = "!!!";
#endif

            if (msg is IUserMessage message && !message.Author.IsBot && message.Author.Id != _client.CurrentUser.Id)
            {
                var context = new SocketCommandContext(_client, msg as SocketUserMessage);

                if (message.HasStringPrefix(prefix, ref argPos) && !char.IsNumber(message.Content[argPos]) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))
                {
                    var result = await _globalCommandService.ExecuteAsync(context, argPos, _serviceProvider);
                    if (!result.IsSuccess)
                    {
                        if (result.Error == CommandError.UnknownCommand)
                        {
                            await msg.Channel.SendMessageAsync($"Unknown command")
                                .ContinueWith(x => x.Result.DelayedDelete(TimeSpan.FromSeconds(10)));
                        }
                        else
                        {
                            await msg.Channel.SendMessageAsync($"```{result.ErrorReason}```").ContinueWith(x => x.Result.DelayedDelete(TimeSpan.FromSeconds(10)));
                        }
                    }
                }
            }
        }

        internal async Task HandleUserJoin(SocketGuildUser user)
        {
            using (var ctx = new DataContext())
            {
                var dbServer = ctx.DiscordServers.SingleOrDefault(x => x.ServerId == (long)user.Guild.Id);
                var dbServerUser = dbServer.Users.FirstOrDefault(x => x.User.UserId == (long)user.Id);
                if (dbServerUser == null)
                {
                    var dbUser = ctx.DiscordUsers.FirstOrDefault(x => x.UserId == (long)user.Id);
                    if (dbUser == null)
                        ctx.DiscordUsers.Add(dbUser = new DiscordUser { UserId = (long)user.Id });
                    dbServer.Users.Add(dbServerUser = new DiscordServerUser { User = dbUser });
                }
                dbServerUser.Nickname = user.Nickname;
                dbServerUser.Roles = dbServer.Roles.Where(x => user.Roles.Any(r => (long)r.Id == x.RoleId)).ToList();
                dbServerUser.User.Username = user.Username;
                dbServerUser.User.Discriminator = user.DiscriminatorValue;
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleUserLeave(SocketGuildUser user)
        {
            using (var ctx = new DataContext())
            {
                var dbServer = ctx.DiscordServers.SingleOrDefault(x => x.ServerId == (long)user.Guild.Id);
                var dbServerUser = dbServer.Users.FirstOrDefault(x => x.User.UserId == (long)user.Id);
                if (dbServerUser == null)
                    return;

                dbServer.Users.Remove(dbServerUser);
                await ctx.SaveChangesAsync();
            }
        }

        internal async Task HandleDominanceUpdate(GenericMessage dominanceUpdate)
        {
            if (dominanceUpdate.Message == "Updated")
            {
                await DominanceHandler.UpdateGuild(_settingManager, Guild);
            }
        }

        internal async Task HandleSettingUpdate(GenericMessage settingUpdate)
        {
            await Task.Run(() => _settingManager.ForceUpdate(Guild, settingUpdate.Message));
        }

        internal Task Available()
        {
            var _ = Task.Run(() =>
            {
                using (var ctx = new DataContext())
                {
                    var dbServer = ctx.DiscordServers.Include(x => x.Users.Select(u => u.User)).Include(x => x.Users.Select(u => u.Roles)).Include(x => x.Roles).Include(x => x.Channels).FirstOrDefault(x => x.ServerId == (long)Guild.Id);
                    if (dbServer == null)
                    {
                        ctx.DiscordServers.Add(dbServer = new DiscordServer());
                        dbServer.ServerId = (long)Guild.Id;
                    }
                    dbServer.Name = Guild.Name;

                    if (Guild.IconUrl != null)
                    {
                        using (var client = new WebClient())
                        {
                            var iconData = client.DownloadData(Guild.IconUrl);
                            dbServer.IconBase64 = Convert.ToBase64String(iconData);
                        }
                    }

                    foreach (var existingRole in dbServer.Roles.ToList())
                    {
                        if (!Guild.Roles.Any(x => (long)x.Id == existingRole.RoleId))
                            dbServer.Roles.Remove(existingRole);
                    }

                    foreach (var role in Guild.Roles)
                    {
                        var dbRole = dbServer.Roles.FirstOrDefault(x => x.RoleId == (long)role.Id);
                        if (dbRole == null)
                        {
                            dbServer.Roles.Add(dbRole = new DiscordRole());
                            dbRole.RoleId = (long)role.Id;
                        }
                        dbRole.Name = role.Name;
                        dbRole.Color = role.Color.RawValue.ToString();
                        dbRole.DiscordPermissions = (long)role.Permissions.RawValue;
                    }

                    foreach (var existingChannel in dbServer.Channels.ToList())
                    {
                        if (!Guild.Channels.Any(x => (long)x.Id == existingChannel.ChannelId))
                            dbServer.Channels.Remove(existingChannel);
                    }

                    foreach (var channel in Guild.Channels)
                    {
                        var dbChannel = dbServer.Channels.FirstOrDefault(x => x.ChannelId == (long)channel.Id);
                        if (dbChannel == null)
                        {
                            dbServer.Channels.Add(dbChannel = new DiscordChannel());
                            dbChannel.ChannelId = (long)channel.Id;
                        }
                        dbChannel.ChannelType = channel is ITextChannel ? DiscordChannelType.Text : channel is IVoiceChannel ? DiscordChannelType.Voice : channel is ICategoryChannel ? DiscordChannelType.Category : DiscordChannelType.Other;
                        dbChannel.Name = channel.Name;
                    }

                    foreach (var user in Guild.Users)
                    {
                        var dbServerUser = dbServer.Users.FirstOrDefault(x => x.User.UserId == (long)user.Id);
                        if (dbServerUser == null)
                        {
                            var dbUser = ctx.DiscordUsers.FirstOrDefault(x => x.UserId == (long)user.Id);
                            if (dbUser == null)
                                ctx.DiscordUsers.Add(dbUser = new DiscordUser { UserId = (long)user.Id });
                            dbServer.Users.Add(dbServerUser = new DiscordServerUser { User = dbUser });
                        }
                        dbServerUser.Nickname = user.Nickname;
                        dbServerUser.Roles = dbServer.Roles.Where(x => user.Roles.Any(r => (long)r.Id == x.RoleId)).ToList();
                        dbServerUser.User.Username = user.Username;
                        dbServerUser.User.Discriminator = user.DiscriminatorValue;
                    }

                    ctx.SaveChanges();
                }
            });

            return Task.CompletedTask;
        }

        #region Unused
        internal Task Unavailable() => Task.CompletedTask;
        internal Task HandleReactionsCleared(Cacheable<IUserMessage, ulong> message, SocketGuildChannel guildChannel) => throw new NotImplementedException();
        internal Task HandleUserBanned(SocketUser user) => Task.CompletedTask;
        internal Task HandleUserUnbanned(SocketUser user) => Task.CompletedTask;
        internal Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, SocketGuildChannel guildChannel, SocketReaction reaction) => Task.CompletedTask;
        internal Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> message, SocketGuildChannel guildChannel, SocketReaction reaction) => Task.CompletedTask;
        internal Task HandlerUserVoiceUpdated(SocketGuildUser guildUser, SocketVoiceState stateOld, SocketVoiceState stateNew) => Task.CompletedTask;
        #endregion
    }
}
