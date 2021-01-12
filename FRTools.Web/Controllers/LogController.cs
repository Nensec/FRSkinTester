using FRTools.Data.DataModels;
using System.Linq;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [Authorize(Roles = "Log")]
    [Authorize(Roles = "Admin")]
    [RoutePrefix("log")]
    public class LogController : BaseController
    {
        [Route("", Name = "LogIndex")]
        public ActionResult Index(LogItemSeverity? severity, LogItemOrigin? origin, int? userid, long? discordUser, long? discordChannel, long? discordServer, string messageQuery, string exceptionQuery, int page = 1, int pageSize = 20)
        {
            var logItems = DataContext.LogItems.AsQueryable();

            if (severity != null)
                logItems = logItems.Where(x => x.Severity == severity);
            if (origin != null)
                logItems = logItems.Where(x => x.Origin == origin);
            if (userid != null)
                logItems = logItems.Where(x => x.User.Id == userid);
            if (discordUser != null)
                logItems = logItems.Where(x => x.DiscordUser.UserId == discordUser);
            if (discordChannel != null)
                logItems = logItems.Where(x => x.Channel.ChannelId == discordChannel);
            if (discordServer != null)
                logItems = logItems.Where(x => x.Server.ServerId == discordServer);

            logItems = logItems.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize);

            return View(logItems.ToList());
        }

        [Route("{logId}", Name = "LogInspect")]
        public ActionResult Inspect(int logId)
        {
            var logItem = DataContext.LogItems.FirstOrDefault(x => x.Id == logId);
            return View(logItem);
        }
    }
}