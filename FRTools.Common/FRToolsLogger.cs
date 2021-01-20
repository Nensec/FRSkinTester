using FRTools.Data;
using FRTools.Data.DataModels;
using NLog;
using NLog.Config;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FRTools.Common
{
    public class FRToolsLogger : Logger
    {
        public LogItemOrigin Origin { get; set; }

        public FRToolsLogger WithOrigin(LogItemOrigin origin)
        {
            Origin = origin;
            return this;
        }

        public static void Setup(LogItemOrigin origin)
        {
            var logConfig = new LoggingConfiguration();

#if DEBUG
            var logFile = new NLog.Targets.FileTarget("logfile") { FileName = "${logger}/${date:universalTime=true:format=yyyy-MM-dd}/${date:universalTime=true:format=HH}.log" };
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logFile);
#else
            var logAzure = new NLog.Targets.BlobStorageTarget
            {
                ConnectionString = System.Configuration.ConfigurationManager.AppSettings["AzureCredentials"],
                Name = "Azure",
                Layout = "${longdate:universalTime=true} ${level:uppercase=true} - ${logger}: ${message} ${exception:format=tostring}",
                BlobName = "${date:universalTime=true:format=yyyy-MM-dd}/${date:universalTime=true:format=HH}.log",
                Container = "logs"
            };
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logAzure);
#endif

            var logConsole = new NLog.Targets.ColoredConsoleTarget("logconsole");
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logConsole);

            LogManager.Configuration = logConfig;
        }

        public void Log(LogItemOrigin origin, LogItemSeverity severity, string message, Exception exception = null, LoggingContext context = null)
        {
            var _ = Task.Run(() =>
            {
                switch (severity)
                {
                    case LogItemSeverity.Debug:
                        Debug(exception, message);
                        break;
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
                        Origin = Origin & origin,
                        Severity = severity,
                        Message = message,
                        Exception = exception?.ToString(),
                        User = context.User
                    });

                    if (context.DiscordUser != null)
                        logItem.DiscordUser = ctx.DiscordUsers.FirstOrDefault(x => x.UserId == context.DiscordUser);
                    if (context.DiscordChannel != null)
                        logItem.Channel = ctx.DiscordChannels.FirstOrDefault(x => x.ChannelId == context.DiscordChannel);
                    if (context.DiscordServer != null)
                        logItem.Server = logItem.Channel?.Server ?? ctx.DiscordServers.FirstOrDefault(x => x.ServerId == context.DiscordServer);

                    ctx.SaveChanges();
                }
            });
        }
    }
}
