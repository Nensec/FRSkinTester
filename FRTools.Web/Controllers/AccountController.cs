﻿using FRTools.Web.Infrastructure;
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

namespace FRTools.Web.Controllers
{
    public class AccountController : BaseController
    {
        private SignInManager<User, int> _signInManager;
        private UserManager<User, int> _userManager;

        public SignInManager<User, int> SignInManager => _signInManager ?? (_signInManager = HttpContext.GetOwinContext().Get<SignInManager<User, int>>());

        public UserManager<User, int> UserManager => _userManager ?? (_userManager = HttpContext.GetOwinContext().GetUserManager<UserManager<User, int>>());

        [Route("login", Name = "Login")]
        public ActionResult Login(string returnUrl) => View(new ExternalLoginListViewModel { ReturnUrl = returnUrl });

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
        public ActionResult ExternalLogin(string provider, string returnUrl) =>
            new ChallengeResult(provider, Url.RouteUrl("ExternalLoginCallback", new { ReturnUrl = returnUrl }));

        [AllowAnonymous]
        [Route("externalLoginCallback", Name = "ExternalLoginCallback")]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                // Since retrying the entire thing again seems to work, maybe just doing this call again will fix the null?
                loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (loginInfo == null)
                {
                    TempData["Error"] = "Could not retrieve external login information from request, please try again.<br/>If the issue persists then please let me know <a href=\"https://github.com/Nensec/FRTools/issues/5\">here</a>.";
                    return RedirectToRoute("Login");
                }
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    if (returnUrl != null)
                        return RedirectToLocal(returnUrl);
                    return RedirectToRoute("Home");
                case SignInStatus.Failure:
                default:
                    if (ModelState.IsValid)
                    {
                        async Task<(IdentityResult, User)> CreateNewUser(string username, string email)
                        {
                            var user = new User
                            {
                                UserName = username,
                                Email = email
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
                                TempData["Error"] = $"Could not login user:<br/><br/><ul>{loginResult.Errors.Select(x => $"<li>{x}</li>")}</ul>";
                                return RedirectToRoute("Login");
                            }
                        };

                        var (newUser, identity) = await CreateNewUser(loginInfo.DefaultUserName ?? loginInfo.ExternalIdentity.FindFirst(ClaimTypes.NameIdentifier).Value, loginInfo.Email);

                        if (newUser.Succeeded)
                            return await LoginUser(identity);
                        else
                        {
                            (newUser, identity) = await CreateNewUser(identity.UserName + GenerateId(5), identity.Email);
                            if (newUser.Succeeded)
                                return await LoginUser(identity);
                            TempData["Error"] = $"Could not create user:<br/><br/><ul>{newUser.Errors.Select(x => $" < li >{ x}</ li > ")}</ul>";
                            return RedirectToRoute("Login");
                        }
                    }

                    TempData["Error"] = "Something went wrong submitting the request";

                    ViewBag.ReturnUrl = returnUrl;
                    return RedirectToRoute("Login");
            }
        }

        private IAuthenticationManager AuthenticationManager => HttpContext.GetOwinContext().Authentication;
    }
}