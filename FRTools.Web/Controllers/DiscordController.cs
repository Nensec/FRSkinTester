using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Data.DataModels.DiscordModels;
using FRTools.Data.Messages;
using FRTools.Web.Infrastructure;
using FRTools.Web.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [RoutePrefix("discord")]
    public class DiscordController : BaseController
    {
        private static DiscordMetadata DiscordMetadata { get; }
        private static long DiscordBotOwnerId { get; }

        static DiscordController()
        {
            var json = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~/bin/DiscordMetadata.json"));
            DiscordMetadata = JsonConvert.DeserializeObject<DiscordMetadata>(json);
            if (long.TryParse(ConfigurationManager.AppSettings["DiscordBotOwner"], out var id))
                DiscordBotOwnerId = id;
        }

        public DiscordController(DataContext dataContext) : base(dataContext)
        {
            ViewBag.PngLogo = "/Content/frtools_discord.png";
            ViewBag.Logo = "/Content/frtools_discord.svg";
        }

        private long? _currentUserId;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Request.IsAuthenticated)
            {
                _currentUserId = Session["CurrentDiscordUser"] as long?;
                if (_currentUserId == null)
                {
                    var owin = HttpContext.GetOwinContext();
                    var userManager = owin.GetUserManager<UserManager<User, int>>();
                    var logins = userManager.GetLogins(HttpContext.User.Identity.GetUserId<int>());
                    var discordLogin = logins.FirstOrDefault(x => x.LoginProvider.ToLower() == "discord");
                    if (discordLogin != null)
                        Session["CurrentDiscordUser"] = _currentUserId = long.Parse(discordLogin.ProviderKey);
                }
            }

            base.OnActionExecuting(filterContext);
        }

        [Route(Name = "DiscordHome")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("help", Name = "DiscordHelp")]
        public ActionResult Help()
        {
            var model = new DiscordHelpViewModel();
            model.Modules = DiscordMetadata.Modules.Where(x => !x.RequireOwner).Select(x => new DiscordModuleHelpViewModel
            {
                Module = x,
                Commands = x.Commands.Select(c => new DiscordCommandHelpViewModel { Command = c }).ToList()
            }).ToList();
            return View(model);
        }

        [Route("help/{module}/{command}")]
        public ActionResult CommandHelp(string module, string command)
        {
            var discordModule = DiscordMetadata.Modules.FirstOrDefault(x => x.Name.ToLower() == module.ToLower());
            if (discordModule != null)
            {
                var moduleCommand = discordModule.Commands.FirstOrDefault(x => x.Name.ToLower() == command.ToLower());
                if (moduleCommand != null)
                {
                    return PartialView("_CommandHelp", moduleCommand);
                }
            }
            return HttpNotFound();
        }

        [Route("manage", Name = "DiscordManage")]
        [Authorize]
        [MustHaveLoginProvider("discord", "DiscordHome")]
        public ActionResult Manage()
        {
            var currentUser = DataContext.DiscordUsers.FirstOrDefault(x => x.UserId == _currentUserId);
            if (currentUser == null)
                TempData["Warning"] = "The bot has not encountered you at all yet in any servers, are you in a mutual server with the bot?";
            return View(new ServersViewModel
            {
                Servers = currentUser?.Servers.Where(x => x.IsOwner || x.Roles.Any(r => (r.DiscordPermissions & 8) != 0)).Select(x => new ServerViewModel
                {
                    ServerId = x.Server.ServerId,
                    UserCount = x.Server.Users.Count,
                    IconBase64 = x.Server.IconBase64,
                    ServerName = x.Server.Name
                }).ToList() ?? new System.Collections.Generic.List<ServerViewModel>(),
                CurrentUser = currentUser
            });
        }

        private ServerViewModel GetServerViewModel(DiscordUser currentUser, long discordServer)
        {
            var server = currentUser.Servers.First(x => x.Server.ServerId == discordServer).Server;
            var serverModel = new ServerViewModel
            {
                ServerId = discordServer,
                Modules = DiscordMetadata.Modules.Where(x => CheckModule(x.Name)).ToList(),
                ServerName = server.Name,
            };
            serverModel.Channels = server.Channels.Select(x => new DiscordChannelViewModel { ChannelId = x.ChannelId, ChannelName = x.Name, ParentServer = serverModel, DiscordChannelType = x.ChannelType }).ToList();
            serverModel.Roles = server.Roles.Where(x => x.Name != "@everyone").Select(x => new DiscordRoleViewModel { RoleId = x.RoleId, RoleName = x.Name, ParentServer = serverModel }).ToList();
            var botSettings = DiscordMetadata.BotSettings.OrderBy(x => x.Type).Select(x => new DiscordSettingViewModel
            {
                Key = x.Key,
                ParentServer = serverModel,
                Value = DataContext.DiscordSettings.FirstOrDefault(s => s.Server.ServerId == discordServer && s.Key == x.Key)?.Value,
                SettingType = x.Type,
                SettingName = x.Name,
                Description = ParseDescription(x),
                ExtraArgs = x.ExtraArgs
            }).ToList();
            serverModel.BotSettings = botSettings;
            return serverModel;
        }

        [Route("manage/{discordServer}", Name = "DiscordManageServer")]
        [Authorize]
        [MustHaveLoginProvider("discord", "DiscordHome")]
        public ActionResult ManageServer(long discordServer)
        {
            var currentUser = DataContext.DiscordUsers.First(x => x.UserId == _currentUserId);

            if (CheckMutualServer(discordServer, currentUser))
                return View(GetServerViewModel(currentUser, discordServer));
            else
                return RedirectToRoute("DiscordManage");
        }

        [Route("manage/{discordServer}/{module}", Name = "DiscordManageModule")]
        [Authorize]
        [MustHaveLoginProvider("discord", "DiscordHome")]
        public ActionResult ManageModule(long discordServer, string module)
        {
            var currentUser = DataContext.DiscordUsers.First(x => x.UserId == _currentUserId);

            if (CheckMutualServer(discordServer, currentUser))
            {
                if (CheckModule(module))
                {
                    var serverModel = GetServerViewModel(currentUser, discordServer);

                    var model = new DiscordModuleViewModel
                    {
                        ParentServer = serverModel,
                        SelectedModule = DiscordMetadata.Modules.First(x => x.Name.ToLower() == module.ToLower())
                    };
                    var moduleSettings = model.SelectedModule.Settings.OrderBy(x => x.Type).Select(x => new DiscordSettingViewModel
                    {
                        Key = x.Key,
                        SettingName = x.Name,
                        Description = ParseDescription(x),
                        ParentServer = serverModel,
                        SettingType = x.Type,
                        Value = DataContext.DiscordSettings.FirstOrDefault(s => s.Server.ServerId == discordServer && s.Key == x.Key)?.Value,
                        ExtraArgs = x.ExtraArgs,
                        Module = module
                    }).ToList();
                    model.ModuleSettings = moduleSettings;
                    return View(model);
                }
                else
                {
                    AddErrorNotification($"Could not find module '{module}', or you do not have access to it on this server");
                    return RedirectToRoute("DiscordManageServer", new { discordServer });
                }
            }
            else
                return RedirectToRoute("DiscordManage");

        }

        private string ParseDescription(Models.DiscordSetting setting)
        {
            var description = setting.Description;
            if (description != null)
            {
                var matches = Regex.Matches(setting.Description, "[$]<([A-Z]*):([A-Z_]*)>");
                foreach (var match in matches.Cast<Match>().Where(x => x.Success))
                {
                    var refSetting = GetRefSetting(match.Groups[1].Value, match.Groups[2].Value);
                    description = description.Replace(match.Groups[0].Value, $"'{refSetting.Name}'");
                }
            }
            return description;
        }

        private static Models.DiscordSetting GetRefSetting(string module, string key)
        {
            var settings = module == "GUILD"
                ? DiscordMetadata.BotSettings
                : DiscordMetadata.Modules.First(x => x.Name.ToUpper() == module).Settings;
            return settings.First(x => x.Key == key);
        }

        [Route("save", Name = "DiscordSaveSetting")]
        [Authorize]
        [MustHaveLoginProvider("discord", "DiscordHome")]
        [HttpPost]
        public async Task<ActionResult> SaveSetting(string discordServer, string module, string key, string value)
        {
            var discordServerId = long.Parse(discordServer);
            ActionResult ErrorResult() => new HttpUnauthorizedResult("User does not have required permission");
            var currentUser = DataContext.DiscordUsers.FirstOrDefault(x => x.UserId == _currentUserId)?.Servers.FirstOrDefault(x => x.Server.ServerId == discordServerId);
            if (currentUser == null)
                return ErrorResult();

            Data.DataModels.DiscordModels.DiscordSetting setting = null;
            if (module == null)
            {
                var botSetting = DiscordMetadata.BotSettings.FirstOrDefault(x => x.Key.ToLower() == key.ToLower());
                if (botSetting == null)
                    return ErrorResult();

                if (currentUser.IsOwner || currentUser.Roles.Any(x => (x.DiscordPermissions & 8) != 0))
                {
                    setting = DataContext.DiscordSettings.FirstOrDefault(x => x.Server.ServerId == currentUser.Server.ServerId && x.Key == botSetting.Key);
                    if (setting == null)
                        setting = DataContext.DiscordSettings.Add(new Data.DataModels.DiscordModels.DiscordSetting { Server = currentUser.Server, Key = botSetting.Key });
                    setting.Value = value;
                }
            }
            else
            {
                var discordModule = DiscordMetadata.Modules.FirstOrDefault(x => x.Name.ToLower() == module.ToLower());
                if (discordModule == null)
                    return ErrorResult();

                var moduleSetting = discordModule.Settings.FirstOrDefault(x => x.Key.ToLower() == key.ToLower());
                if (moduleSetting == null)
                    return ErrorResult();

                if (currentUser.IsOwner || currentUser.Roles.Any(x => (x.DiscordPermissions & 8) != 0))
                {
                    setting = DataContext.DiscordSettings.FirstOrDefault(x => x.Server.ServerId == currentUser.Server.ServerId && x.Key == moduleSetting.Key);
                    if (setting == null)
                        setting = DataContext.DiscordSettings.Add(new Data.DataModels.DiscordModels.DiscordSetting { Server = currentUser.Server, Key = moduleSetting.Key });
                    setting.Value = value;
                }
            }

            DataContext.SaveChanges();
            var _serviceBus = new QueueClient(ConfigurationManager.AppSettings["AzureSBConnString"], ConfigurationManager.AppSettings["AzureSBQueueName"]);
            await _serviceBus.SendAsync(new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new GenericMessage(MessageCategory.SettingUpdated, setting.Key) { DiscordServer = discordServerId }))));

            return Json(JsonConvert.SerializeObject(new { result = 1, setting.Key, setting.Value }));
        }

        private bool CheckMutualServer(long discordServer, DiscordUser currentUser)
        {
            if (currentUser.Servers.Where(x => x.IsOwner || x.Roles.Any(r => (r.DiscordPermissions & 8) != 0)).Select(x => x.Server).Any(x => x.ServerId == discordServer))
                return true;
            else
            {
                AddErrorNotification($"Could not find server id '{discordServer}' in the list of mutual servers where you are an administrator between you and the bot");
                return false;
            }
        }

        private bool CheckModule(string module)
        {
            var discordModule = DiscordMetadata.Modules.FirstOrDefault(x => x.Name.ToLower() == module.ToLower());
            if (discordModule == null)
                return false;

            if (discordModule.RequireOwner && _currentUserId != DiscordBotOwnerId)
                return false;

            return true;
        }
    }
}