using FRTools.Web.Infrastructure;
using FRTools.Data.DataModels;
using FRTools.Web.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Net;
using Newtonsoft.Json;
using FRTools.Web.Infrastructure.Managers;
using FRTools.Data;
using FRTools.Common;

namespace FRTools.Web.Controllers
{
    [RoutePrefix("account")]
    public class AccountController : BaseController
    {
        private IAuthenticationManager AuthenticationManager => HttpContext.GetOwinContext().Authentication;
        private SignInManager<User, int> _signInManager;
        private UserManager<User, int> _userManager;

        public AccountController(DataContext dataContext, FRToolsLogger logger) : base(dataContext, logger)
        {
        }

        public SignInManager<User, int> SignInManager => _signInManager ?? (_signInManager = HttpContext.GetOwinContext().Get<SignInManager<User, int>>());

        public UserManager<User, int> UserManager => _userManager ?? (_userManager = HttpContext.GetOwinContext().GetUserManager<UserManager<User, int>>());

        [Route("login", Name = "Login")]
        public ActionResult Login(string returnUrl = null) => View(new ExternalLoginListViewModel { ReturnUrl = returnUrl });

        [Route("logout", Name = "LogOut")]
        public ActionResult LogOut()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToRoute("Home");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("externalLogin")]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            return new ChallengeResult(provider, Url.RouteUrl("ExternalLoginCallback", new { ReturnUrl = returnUrl }));
        }

        private static long GetDiscordUserId(ExternalLoginInfo loginInfo)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.Authorization, $"Bearer {loginInfo.ExternalIdentity.FindFirstValue("urn:discord:accesstoken")}");
                return JsonConvert.DeserializeObject<dynamic>(client.DownloadString("https://discordapp.com/api/users/@me")).id;
            }
        }

        [AllowAnonymous]
        [Route("externalLoginCallback", Name = "ExternalLoginCallback")]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl, string error)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                // Since retrying the entire thing again seems to work, maybe just doing this call again will fix the null?
                loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (loginInfo == null)
                {
                    AddErrorNotification("Could not retrieve external login information from request, please try again.<br/>If the issue persists then please let me know <a href=\"https://github.com/Nensec/FRTools/issues/5\">here</a>.");
                    return RedirectToRoute("Login");
                }
            }
            loginInfo.Login.ProviderKey = GetProviderData(loginInfo);

            // Sign in the user with this external login provider if the user already has a login
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    {
                        var user = await UserManager.FindAsync(loginInfo.Login);
                        var providerData = JsonConvert.DeserializeObject<UserStore.ProviderData>(loginInfo.Login.ProviderKey);
                        var providerLogin = user.Logins.First(x => x.ProviderKey == providerData.ProviderKey && x.LoginProvider == loginInfo.Login.LoginProvider);
                        if (providerLogin.ProviderUsername == null)
                        {
                            providerLogin.ProviderUsername = providerData.ProviderUsername;
                            await UserManager.UpdateAsync(user);
                        }
                        if (returnUrl != null)
                            return RedirectToLocal(returnUrl);
                        return RedirectToRoute("Home");
                    }
                case SignInStatus.Failure:
                default:
                    if (ModelState.IsValid)
                    {
                        async Task<(IdentityResult, User)> CreateNewUser(ExternalLoginInfo externalLoginInfo, string usernameAffix = null)
                        {
                            var providerData = JsonConvert.DeserializeObject<UserStore.ProviderData>(externalLoginInfo.Login.ProviderKey);
                            var user = new User
                            {
                                UserName = providerData.ProviderUsername + usernameAffix,
                                Email = externalLoginInfo.Email,
                                ProfileSettings = new ProfileSettings()
                            };

                            return (await UserManager.CreateAsync(user), user);
                        };

                        async Task<ActionResult> LoginUser(User user)
                        {

                            var loginResult = await UserManager.AddLoginAsync(user.Id, loginInfo.Login);
                            if (loginResult.Succeeded)
                            {
                                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                                return RedirectToLocal(returnUrl);
                            }
                            else
                            {
                                AddErrorNotification($"Could not login user:<br/><br/><ul>{string.Join("", loginResult.Errors.Select(x => $"<li>{x}</li>"))}</ul>");
                                return RedirectToRoute("Login");
                            }
                        };

                        string externalUsername = loginInfo.DefaultUserName ?? loginInfo.ExternalIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

                        var (newUser, identity) = await CreateNewUser(loginInfo);

                        if (newUser.Succeeded)
                            return await LoginUser(identity);
                        else if (!identity.UserName.All(x => char.IsDigit(x)))
                        {
                            (newUser, identity) = await CreateNewUser(loginInfo, CodeHelpers.GenerateId(5));
                            if (newUser.Succeeded)
                                return await LoginUser(identity);
                            AddErrorNotification($"Could not create user:<br/><br/><ul>{string.Join("", newUser.Errors.Select(x => $"<li>{x}</li>"))}</ul>");
                            return RedirectToRoute("Login");
                        }
                    }

                    AddErrorNotification("Something went wrong submitting the request");

                    ViewBag.ReturnUrl = returnUrl;
                    return RedirectToRoute("Login");
            }
        }

        [Route("removeLogin", Name = "ManageRemoveLogin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            var result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId<int>(), new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }

                AddSuccessNotification($"Succesfully removed the specified {loginProvider} login from your account!");
            }
            else
                AddErrorNotification(result.Errors.First());
            return RedirectToRoute("ManageLogins");
        }

        [Route("externalLinkLogin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider)
        {
            return new ChallengeResult(provider, Url.RouteUrl("ExternalLoginLinkCallback"), User.Identity.GetUserId());
        }

        [Route("externalLoginLinkCallback", Name = "ExternalLoginLinkCallback")]
        public async Task<ActionResult> LinkLoginCallback(string error)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(ChallengeResult._xsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                AddErrorNotification("Could not retrieve external login information from request, please try again.<br/>If the issue persists then please let me know <a href=\"https://github.com/Nensec/FRTools/issues/5\">here</a>.");
                return RedirectToRoute("ManageLogins");
            }

            loginInfo.Login.ProviderKey = GetProviderData(loginInfo);

            var result = await UserManager.AddLoginAsync(User.Identity.GetUserId<int>(), loginInfo.Login);
            if (!result.Succeeded)
                AddErrorNotification(result.Errors.First());
            else
                AddSuccessNotification($"Succesfully added your {loginInfo.Login.LoginProvider} login to your account!");

            return RedirectToRoute("ManageLogins");
        }

        private string GetProviderData(ExternalLoginInfo loginInfo)
        {
            var providerData = new UserStore.ProviderData();
            switch (loginInfo.Login.LoginProvider.ToLower())
            {
                case "discord":
                    providerData.ProviderKey = GetDiscordUserId(loginInfo).ToString();
                    providerData.ProviderUsername = $"{loginInfo.ExternalIdentity.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")}#{loginInfo.ExternalIdentity.FindFirstValue("urn:discord:discriminator")}";
                    break;
                default:
                    providerData.ProviderKey = loginInfo.Login.ProviderKey;
                    providerData.ProviderUsername = loginInfo.DefaultUserName ?? loginInfo.ExternalIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                    break;
            }
            return JsonConvert.SerializeObject(providerData);
        }

        [Route("logins", Name = "ManageLogins")]
        [Authorize]
        public async Task<ActionResult> ManageLogins()
        {
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());

            return View(new ManageLoginsViewModel
            {
                CurrentLogins = user.Logins.ToList(),
                ShowRemoveButton = user.Logins.Count > 1
            });
        }
    }
}