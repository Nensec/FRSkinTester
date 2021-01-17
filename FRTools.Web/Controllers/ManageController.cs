using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Web.Infrastructure;
using FRTools.Web.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{

    [Authorize]
    [RoutePrefix("manage")]
    public class ManageController : BaseController
    {
        // Opting to just store the guids in memory rather than save them in the database
        private static MemoryCache _verifyCache = new MemoryCache("verifyCache");

        private SignInManager<User, int> _signInManager;
        private UserManager<User, int> _userManager;

        public SignInManager<User, int> SignInManager => _signInManager ?? (_signInManager = HttpContext.GetOwinContext().Get<SignInManager<User, int>>());

        public UserManager<User, int> UserManager => _userManager ?? (_userManager = HttpContext.GetOwinContext().GetUserManager<UserManager<User, int>>());

        public ManageController(DataContext dataContext, FRToolsLogger logger) : base(dataContext, logger)
        {
        }

        [Route(Name = "ManageAccount")]
        public ActionResult Index()
        {
            var model = new AccountViewModel { User = LoggedInUser };
            return View(model);
        }

        [Route("verify", Name = "VerifyFR")]
        public ActionResult Verify()
        {
            var userId = HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>();
            Guid? verifyGuid = Guid.NewGuid();
            verifyGuid = (Guid?)_verifyCache.AddOrGetExisting($"USER_{userId}", verifyGuid, DateTimeOffset.Now.AddMinutes(5)) ?? verifyGuid;

            return View(new VerifyFRViewModel { Guid = verifyGuid.Value });
        }

        [Route("verify", Name = "VerifyFRPost")]
        [HttpPost]
        public async Task<ActionResult> Verify(VerifyFRPostViewModel model)
        {
            using (var ctx = new DataContext())
            {
                var frUser = await FRHelpers.GetOrUpdateFRUser(model.LairId, ctx);

                if (frUser.User != null)
                {
                    AddErrorNotification("This lair id is already tied to an account, if you believe this is an error please contact Nensec#1337 on Discord");
                    RedirectToRoute("VerifyFR");
                }

                var userId = HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>();

                var key = $"USER_{userId}";
                if (_verifyCache.Contains(key))
                {
                    var verifyGuid = (Guid)_verifyCache.Get(key);
                    using (var client = new WebClient())
                    {
                        var profilePage = await client.DownloadStringTaskAsync($@"https://www1.flightrising.com/clan-profile/{model.LairId}");
                        var bioTagIndex = profilePage.IndexOf("<div id=\"userbio\">");
                        if (profilePage.Substring(bioTagIndex).Contains(verifyGuid.ToString()))
                        {
                            _verifyCache.Remove(key);
                            {
                                var dbUser = ctx.Users.Find(userId);
                                dbUser.FRUser = frUser;

                                await ctx.SaveChangesAsync();
                            }

                            AddSuccessNotification($"Succesfully linked your Flight Rising account ({frUser.Username})!");
                            return RedirectToRoute("ManageAccount");
                        }
                        else
                        {
                            AddErrorNotification("Verify token was not found in your bio, please double check you saved!");
                            return RedirectToRoute("VerifyFR");
                        }
                    }
                }
                else
                {
                    AddErrorNotification("Verify token expired, please try again!");
                    return RedirectToRoute("VerifyFR");
                }
            }
        }

        [Route("createShareUrl", Name = "GetShareUrl")]
        
        public async Task<ActionResult> GetShareUrl(string type, string id)
        {
            switch (type)
            {
                case "skin":
                    var skin = DataContext.Skins.FirstOrDefault(x => x.GeneratedId == id);
                    if (skin != null)
                    {
                        skin.ShareUrl = await BitlyHelper.TryGenerateUrl(Url.RouteUrl("Preview", new { skinId = id }, "https"));
                        DataContext.SaveChanges();
                        return Json(skin.ShareUrl, JsonRequestBehavior.AllowGet);
                    }
                    goto default;
                case "pinglist":
                    var pinglist = DataContext.Pinglists.FirstOrDefault(x => x.GeneratedId == id);
                    if(pinglist != null)
                    {
                        pinglist.ShareUrl = await BitlyHelper.TryGenerateUrl(Url.RouteUrl("PinglistDirect", new { listId = id }, "https"));
                        DataContext.SaveChanges();
                        return Json(pinglist.ShareUrl, JsonRequestBehavior.AllowGet);
                    }
                    goto default;
                default:
                    return HttpNotFound();
            }
        }
    }
}