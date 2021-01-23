using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Web.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [Authorize(Roles = "Log")]
    [Authorize(Roles = "Admin")]
    [RoutePrefix("log")]
    public class LogController : BaseController
    {
        public LogController(DataContext dataContext, FRToolsLogger logger) : base(dataContext, logger)
        {
        }

        [Route("", Name = "LogIndex")]
        public ActionResult Index(LogsViewModel model)
        {
            var logItems = DataContext.LogItems.AsQueryable();

            if (model.StartDate != null)
                logItems = logItems.Where(x => x.CreatedAt >= model.StartDate);
            if (model.EndDate != null)
                logItems = logItems.Where(x => x.CreatedAt <= model.EndDate);
            if (model.Severity != null)
                logItems = logItems.Where(x => x.Severity == model.Severity);
            if (model.Origin != null)
                logItems = logItems.Where(x => x.Origin == model.Origin);
            if (model.UserId != null)
                logItems = logItems.Where(x => x.User.Id == model.UserId);
            if (model.DiscordUser != null)
                logItems = logItems.Where(x => x.DiscordUser.UserId == model.DiscordUser);
            if (model.DiscordChannel != null)
                logItems = logItems.Where(x => x.Channel.ChannelId == model.DiscordChannel);
            if (model.DiscordServer != null)
                logItems = logItems.Where(x => x.Server.ServerId == model.DiscordServer);
            if (model.MessageQuery != null)
                logItems = logItems.Where(x => x.Message.Contains(model.MessageQuery));
            if (model.ExceptionQuery != null)
                logItems = logItems.Where(x => x.Exception.Contains(model.ExceptionQuery));

            switch (model.OrderBy)
            {
                case "date":
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.CreatedAt) : logItems.OrderBy(x => x.CreatedAt);
                    break;
                case "severity":
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.Severity) : logItems.OrderBy(x => x.Severity);
                    break;
                case "origin":
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.Origin) : logItems.OrderBy(x => x.Origin);
                    break;
                case "userid":
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.User.Id) : logItems.OrderBy(x => x.User.Id);
                    break;
                case "discorduser":
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.DiscordUser.Username) : logItems.OrderBy(x => x.DiscordUser.Username);
                    break;
                case "discordchannel":
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.Channel.Name) : logItems.OrderBy(x => x.Channel.Name);
                    break;
                case "discordserver":
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.Server.Name) : logItems.OrderBy(x => x.Server.Name);
                    break;
                case "id":
                default:
                    logItems = model.OrderByDescending ? logItems.OrderByDescending(x => x.Id) : logItems.OrderBy(x => x.Id);
                    break;
            }

            logItems.Skip((model.Page - 1) * model.PageSize).Take(model.PageSize);

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