using FRTools.Data;
using FRTools.Data.DataModels;
using NLog;
using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace FRTools.Common
{
    public class FRToolsLogger : Logger
    {
        public static void Setup()
        {
            var logConfig = new NLog.Config.LoggingConfiguration();

#if DEBUG
            var logFile = new NLog.Targets.FileTarget("logfile") { FileName = "${logger}/${date:universalTime=true:format=yyyy-MM-dd}/${date:universalTime=true:format=HH}.log" };
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logFile);

            var logAzure = new NLog.Targets.BlobStorageTarget
            {
                ConnectionString = ConfigurationManager.AppSettings["AzureCredentials"],
                Name = "Azure",
                Layout = "${longdate:universalTime=true} ${level:uppercase=true} - ${logger}: ${message} ${exception:format=tostring}",
                BlobName = "${date:universalTime=true:format=yyyy-MM-dd}/${date:universalTime=true:format=HH}.log",
                Container = "logs"
            };
#else
            var logAzure = new NLog.Targets.BlobStorageTarget
            {
                ConnectionString = ConfigurationManager.AppSettings["AzureCredentials"],
                Name = "Azure",
                Layout = "${longdate:universalTime=true} ${level:uppercase=true} - ${logger}: ${message} ${exception:format=tostring}",
                BlobName = "${date:universalTime=true:format=yyyy-MM-dd}/${date:universalTime=true:format=HH}.log",
                Container = "logs"
            };
#endif
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logAzure);

            var logConsole = new NLog.Targets.ColoredConsoleTarget("logconsole");
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logConsole);

            LogManager.Configuration = logConfig;
        }

        public void Log(LogItemOrigin origin, LogItemSeverity severity, string message, Exception exception = null, User user = null, long? discordUser = null, long? discordServer = null, long? discordChannel = null)
        {
            var _ = Task.Run(() =>
            {
                switch (severity)
                {
                    case LogItemSeverity.Info:
                        Info(exception, message);
                        break;
                    case LogItemSeverity.Warning:
                        Warn(exception, message);
                        break;
                    case LogItemSeverity.Error:
                        Error(exception, message);
                        break;
                }

                using (var ctx = new DataContext())
                {
                    var logItem = ctx.LogItems.Add(new LogItem
                    {
                        Origin = origin,
                        Severity = severity,
                        Message = message,
                        Exception = exception?.ToString(),
                        User = user
                    });

                    if (discordUser != null)
                        logItem.DiscordUser = ctx.DiscordUsers.FirstOrDefault(x => x.UserId == discordUser);
                    if (discordChannel != null)
                        logItem.Channel = ctx.DiscordChannels.FirstOrDefault(x => x.ChannelId == discordChannel);
                    if (discordServer != null)
                        logItem.Server = logItem.Channel?.Server ?? ctx.DiscordServers.FirstOrDefault(x => x.ServerId == discordServer);

                    ctx.SaveChanges();
                }
            });
        }
    }
}
