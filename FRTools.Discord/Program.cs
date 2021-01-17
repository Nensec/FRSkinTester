using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Data.Messages;
using FRTools.Discord.Handlers;
using FRTools.Discord.Infrastructure;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity;
using Unity.Lifetime;
using Unity.Resolution;

namespace FRTools.Discord
{
    class Program
    {
        private static DiscordSocketClient _client;
        private static IUnityContainer _container = new UnityContainer();
        private static CommandService _commandService;
        private static readonly Dictionary<ulong, GuildHandler> _handlers = new Dictionary<ulong, GuildHandler>();
        private static readonly UnityServiceProvider _unityServiceProvider = new UnityServiceProvider(_container);
        private static IQueueClient _serviceBus;
        private static FRToolsLogger _logger;

        static async Task Main()
        {
            FRToolsLogger.Setup(LogItemOrigin.Discord);

            using (var ctx = new DataContext())
                ctx.Database.Initialize(false);

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel =
#if DEBUG
                LogSeverity.Debug
#else
                LogSeverity.Warning
#endif
            });
            _serviceBus = new QueueClient(ConfigurationManager.AppSettings["AzureSBConnString"], ConfigurationManager.AppSettings["AzureSBQueueName"]);

            _container.RegisterInstance(_commandService = new CommandService());
            _container.RegisterInstance<IDiscordClient>(_client);
            _container.RegisterInstance(_client);
            _container.RegisterInstance<IServiceProvider>(_unityServiceProvider);
            _container.RegisterSingleton<SettingManager>();
            _container.RegisterInstance(new InteractiveService(_client, new InteractiveServiceConfig { DefaultTimeout = TimeSpan.FromSeconds(15) }));
            _container.RegisterInstance(_serviceBus);
            _container.RegisterType<DataContext>();
            _container.RegisterFactory<FRToolsLogger>(x => LogManager.LogFactory.GetLogger<FRToolsLogger>("Discord").WithOrigin(LogItemOrigin.Discord));

            _logger = _container.Resolve<FRToolsLogger>();
            _client.Log += Client_Log;

            await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), _unityServiceProvider);

            _client.MessageReceived += Client_MessageReceived;
            _client.JoinedGuild += guild => Task.Run(() => _handlers.Add(guild.Id, _container.Resolve<GuildHandler>(new ParameterOverride("guild", guild)))).ContinueWith(x => _handlers[guild.Id].Available());
            _client.LeftGuild += guild => Task.Run(() => _handlers.Remove(guild.Id));
            _client.GuildAvailable += guild => _handlers.TryGetValue(guild.Id, out var handler) ? handler.Available() : Task.Run(() => _handlers.Add(guild.Id, handler = _container.Resolve<GuildHandler>(new ParameterOverride("guild", guild)))).ContinueWith(x => handler.Available());
            _client.GuildUnavailable += async guild => await (_handlers.TryGetValue(guild.Id, out var handler) ? handler.Unavailable() : Task.CompletedTask);
            _client.RoleCreated += async role => await (_handlers.TryGetValue(role.Guild.Id, out var handler) ? handler.HandleRoleCreated(role) : Task.CompletedTask);
            _client.RoleDeleted += async role => await (_handlers.TryGetValue(role.Guild.Id, out var handler) ? handler.HandleRoleDeleted(role) : Task.CompletedTask);
            _client.RoleUpdated += async (roleOld, roleNew) => await (_handlers.TryGetValue(roleOld.Guild.Id, out var handler) ? handler.HandleRoleUpdate(roleOld, roleNew) : Task.CompletedTask);
            _client.GuildMemberUpdated += async (userOld, userNew) => await (_handlers.TryGetValue(userOld.Guild.Id, out var handler) ? handler.HandleMemberUpdate(userOld, userNew) : Task.CompletedTask);
            _client.GuildUpdated += async (guildOld, guildNew) => await (_handlers.TryGetValue(guildOld.Id, out var handler) ? handler.HandleUpdateGuild(guildOld, guildNew) : Task.CompletedTask);
            _client.UserJoined += async user => await (_handlers.TryGetValue(user.Guild.Id, out var handler) ? handler.HandleUserJoin(user) : Task.CompletedTask);
            _client.UserLeft += async user => await (_handlers.TryGetValue(user.Guild.Id, out var handler) ? handler.HandleUserLeave(user) : Task.CompletedTask);
            _client.UserBanned += async (user, guild) => await (_handlers.TryGetValue(guild.Id, out var handler) ? handler.HandleUserBanned(user) : Task.CompletedTask);
            _client.UserUnbanned += async (user, guild) => await (_handlers.TryGetValue(guild.Id, out var handler) ? handler.HandleUserUnbanned(user) : Task.CompletedTask);
            _client.ChannelCreated += async channel => await (channel is SocketGuildChannel guildChannel && _handlers.TryGetValue(guildChannel.Guild.Id, out var handler) ? handler.HandleChannelCreated(guildChannel) : Task.CompletedTask);
            _client.ChannelDestroyed += async channel => await (channel is SocketGuildChannel guildChannel && _handlers.TryGetValue(guildChannel.Guild.Id, out var handler) ? handler.HandleChannelRemoved(guildChannel) : Task.CompletedTask);
            _client.ChannelUpdated += async (channelOld, channelNew) => await (channelOld is SocketGuildChannel guildChannelOld && _handlers.TryGetValue(guildChannelOld.Guild.Id, out var handler) ? handler.HandleChannelUpdated(guildChannelOld, channelNew as SocketGuildChannel) : Task.CompletedTask);
            _client.MessageUpdated += async (messageOld, messageNew, channel) => await ((await messageOld.GetOrDownloadAsync())?.CreatedAt.AddMinutes(1) > messageNew?.EditedTimestamp ? Client_MessageReceived(messageNew) : Task.CompletedTask);
            _client.ReactionAdded += async (message, channel, reaction) => await (channel is SocketGuildChannel guildChannel && _handlers.TryGetValue(guildChannel.Guild.Id, out var handler) ? handler.HandleReactionAdded(message, guildChannel, reaction) : Task.CompletedTask);
            _client.ReactionRemoved += async (message, channel, reaction) => await (channel is SocketGuildChannel guildChannel && _handlers.TryGetValue(guildChannel.Guild.Id, out var handler) ? handler.HandleReactionRemoved(message, guildChannel, reaction) : Task.CompletedTask);
            _client.ReactionsCleared += async (message, channel) => await (channel is SocketGuildChannel guildChannel && _handlers.TryGetValue(guildChannel.Guild.Id, out var handler) ? handler.HandleReactionsCleared(message, guildChannel) : Task.CompletedTask);
            _client.UserUpdated += Client_UserUpdated;
            //client.Disconnected += async ex => await Client_Disconnected(ex);
            //client.Connected += async () => await Client_Connected();
            _client.UserVoiceStateUpdated += async (user, stateOld, stateNew) => await ((user is SocketGuildUser guildUser) ? _handlers.TryGetValue(guildUser.Guild.Id, out var handler) ? handler.HandlerUserVoiceUpdated(guildUser, stateOld, stateNew) : Task.CompletedTask : Task.CompletedTask);

            await _client.LoginAsync(TokenType.Bot, ConfigurationManager.AppSettings["DiscordToken"]);
            await _client.StartAsync();

            _serviceBus.RegisterMessageHandler(ServiceBusMessageHandler, new MessageHandlerOptions(ServiceBusExceptionHandler) { AutoComplete = false, MaxConcurrentCalls = 1 });

            await Task.Delay(-1);
        }

        private static async Task Client_UserUpdated(SocketUser userOld, SocketUser userNew)
        {
            using (var ctx = new DataContext())
            {
                var dbUser = ctx.DiscordUsers.FirstOrDefault(x => x.UserId == (long)userNew.Id);
                if (dbUser == null)
                    ctx.DiscordUsers.Add(dbUser = new Data.DataModels.DiscordModels.DiscordUser { UserId = (long)userNew.Id });
                dbUser.Username = userNew.Username;
                dbUser.Discriminator = userNew.DiscriminatorValue;

                await ctx.SaveChangesAsync();
            }
        }

        private static Task ServiceBusExceptionHandler(ExceptionReceivedEventArgs ex) => Task.Run(() => Console.WriteLine(ex.Exception.ToString()));

        private async static Task ServiceBusMessageHandler(Message msg, CancellationToken cancellationToken)
        {
            var genericMessage = JsonConvert.DeserializeObject<GenericMessage>(Encoding.UTF8.GetString(msg.Body));
            if (genericMessage.MessageType != nameof(GenericMessage))
            {
                var customMessage = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(msg.Body), Assembly.GetAssembly(typeof(GenericMessage)).GetType(nameof(genericMessage.MessageType)));
                foreach (var handler in _handlers.Values)
                {
                    // Handle custom message
                }
            }
            else
            {
                foreach (var handler in _handlers.Values)
                {
                    if (genericMessage.Category == MessageCategory.DominanceTracker)
                        await handler.HandleDominanceUpdate(genericMessage);
                    if (genericMessage.Category == MessageCategory.SettingUpdated && (long)handler.Guild.Id == genericMessage.DiscordServer)
                        await handler.HandleSettingUpdate(genericMessage);
                }
            }

            await _serviceBus.CompleteAsync(msg.SystemProperties.LockToken);
        }

        private static Task Client_Log(LogMessage msg)
        {
            return Task.Run(() =>
            {
                switch (msg.Severity)
                {
                    case LogSeverity.Critical:
                    case LogSeverity.Error:
                        _logger.Log(Data.DataModels.LogItemOrigin.Discord, Data.DataModels.LogItemSeverity.Error, msg.Message, msg.Exception);
                        break;
                    case LogSeverity.Warning:
                        _logger.Log(Data.DataModels.LogItemOrigin.Discord, Data.DataModels.LogItemSeverity.Warning, msg.Message, msg.Exception);
                        break;
                    default:
                        _logger.Log(Data.DataModels.LogItemOrigin.Discord, Data.DataModels.LogItemSeverity.Info, msg.Message, msg.Exception);
                        break;
                }
            });
        }

        private static async Task Client_MessageReceived(SocketMessage msg)
        {
            if (msg.Channel is IGuildChannel channel && _handlers.TryGetValue(channel.GuildId, out var handler))
                await handler.HandleMessage(msg);
            else
            {
                var argPos = 0;
                var prefix = "$";
#if DEBUG
                prefix = "!!";
#endif
                if ((((IUserMessage)msg).HasStringPrefix(prefix, ref argPos) && !char.IsNumber(msg.Content[argPos])) || ((IUserMessage)msg).HasMentionPrefix(_client.CurrentUser, ref argPos))
                {
                    if (msg is IUserMessage && msg.Author.Id != _client.CurrentUser.Id)
                    {
                        var context = new FRToolsCommandContext(_client, msg as SocketUserMessage, _container.Resolve<FRToolsLogger>());
                        var command = _commandService.ExecuteAsync(context, argPos, _unityServiceProvider);
                        var result = await command;

                        if (!result.IsSuccess)
                        {
                            if (result.Error == CommandError.UnknownCommand)
                            {
                                await msg.Channel.SendMessageAsync($"Unknown command")
                                    .ContinueWith(x => x.Result.DelayedDelete(TimeSpan.FromSeconds(10)));
                            }
                            else
                            {
                                await msg.Channel.SendMessageAsync($"```{result.ErrorReason}```");
                            }
                        }
                    }
                }
            }
        }
    }
}