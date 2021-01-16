using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Web.Models;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [RoutePrefix("profile")]
    [Authorize]
    public class ProfileController : BaseController
    {
        private readonly string[] _blacklist = new[] { "edit", "manage" };
        private readonly SkinTester _skinTester;

        public ProfileController(DataContext dataContext, SkinTester skinTester) : base(dataContext)
        {
            _skinTester = skinTester;
        }

        [Route(Name = "SelfProfile")]
        public async Task<ActionResult> Index()
        {
            var vm = new ViewProfileViewModel
            {
                User = LoggedInUser,
                Previews = LoggedInUser.Previews.ToList(),
                Skins = LoggedInUser.Skins.ToList(),
                Pinglists = LoggedInUser.Pinglists.ToList(),
                IsOwn = true,
                GetDummyPreviewImage = (skinId, version) => _skinTester.GenerateOrFetchDummyPreview(skinId, version).GetAwaiter().GetResult().Urls[0]
            };
            return View(vm);
        }

        [Route("{*username}", Name = "Profile")]
        [AllowAnonymous]
        public async Task<ActionResult> Index(string username)
        {
            var user = DataContext.Users.Include(x => x.FRUser).Include(x => x.Previews.Select(p => p.Skin)).FirstOrDefault(x => x.UserName.ToLower() == username.ToLower());
            if (user == null)
            {
                AddErrorNotification($"No user with username '{username}' could be found");
                return RedirectToRoute("Home");
            }
            else if (!user.ProfileSettings.PublicProfile)
            {
                AddErrorNotification("This user's profile is set to private");
                return RedirectToRoute("Home");
            }
            var vm = new ViewProfileViewModel
            {
                User = user,
                GetDummyPreviewImage = (skinId, version) => _skinTester.GenerateOrFetchDummyPreview(skinId, version).GetAwaiter().GetResult().Urls[0]
            };

            if (user.ProfileSettings.ShowPreviewsOnProfile)
                vm.Previews = user.Previews.ToList();
            if (user.ProfileSettings.ShowSkinsOnProfile)
            {
                vm.Skins = user.Skins.Where(x => x.Visibility == SkinVisiblity.Visible || x.Visibility == SkinVisiblity.HideFromBrowse).ToList();
            }
            if (user.ProfileSettings.ShowPingListsOnProfile)
            {
                vm.Pinglists = user.Pinglists.Where(x => x.IsPublic || x.Entries.Any(e => e.FRUser.User?.Id == LoggedInUser.Id)).ToList();
            }
            return View(vm);
        }

        [Route("edit", Name = "EditProfile")]
        public ActionResult Edit()
        {
            var vm = new EditProfileViewModel
            {
                Username = LoggedInUser.UserName,
                DefaultShowSkinsInBrowse = LoggedInUser.ProfileSettings.DefaultShowSkinsInBrowse,
                DefaultSkinsArePublic = LoggedInUser.ProfileSettings.DefaultSkinsArePublic,
                ProfileBio = LoggedInUser.ProfileSettings.ProfileBio,
                PublicProfile = LoggedInUser.ProfileSettings.PublicProfile,
                ShowPingListsOnProfile = LoggedInUser.ProfileSettings.ShowPingListsOnProfile,
                ShowFRLinkStatus = LoggedInUser.ProfileSettings.ShowFRLinkStatus,
                ShowPreviewsOnProfile = LoggedInUser.ProfileSettings.ShowPreviewsOnProfile,
                ShowSkinsOnProfile = LoggedInUser.ProfileSettings.ShowSkinsOnProfile,
                ShowAds = LoggedInUser.ProfileSettings.ShowAds
            };
            return View(vm);
        }

        [HttpPost]
        [Route("edit", Name = "EditProfilePost")]
        public ActionResult Edit(EditProfileViewModel model)
        {
            try
            {
                LoggedInUser.UserName = string.IsNullOrWhiteSpace(model.Username) || _blacklist.Contains(model.Username.ToLower()) ? LoggedInUser.UserName : model.Username;
                LoggedInUser.ProfileSettings.PublicProfile = model.PublicProfile;
                LoggedInUser.ProfileSettings.ProfileBio = model.ProfileBio;
                LoggedInUser.ProfileSettings.DefaultShowSkinsInBrowse = model.DefaultShowSkinsInBrowse;
                LoggedInUser.ProfileSettings.DefaultSkinsArePublic = model.DefaultSkinsArePublic;
                LoggedInUser.ProfileSettings.ShowFRLinkStatus = model.ShowFRLinkStatus;
                LoggedInUser.ProfileSettings.ShowPingListsOnProfile = model.ShowPingListsOnProfile;
                LoggedInUser.ProfileSettings.ShowPreviewsOnProfile = model.ShowPreviewsOnProfile;
                LoggedInUser.ProfileSettings.ShowSkinsOnProfile = model.ShowSkinsOnProfile;
                LoggedInUser.ProfileSettings.ShowAds = model.ShowAds;
                DataContext.SaveChanges();
                AddSuccessNotification("Your profile has been updated!");
            }
            catch (Exception ex)
            {
                var actualException = ex;
                while (actualException.InnerException != null)
                    actualException = actualException.InnerException;

                if (actualException is SqlException sqlEx && sqlEx.Number == 2601)
                    AddErrorNotification("That username is already taken, please pick a different one");
                else
                    AddErrorNotification("Something went wrong with your request");
                return View();
            }
            return RedirectToRoute("SelfProfile");
        }
    }
}