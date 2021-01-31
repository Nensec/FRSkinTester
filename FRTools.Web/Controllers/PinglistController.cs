using FRTools.Common;
using FRTools.Common.Jobs;
using FRTools.Data;
using FRTools.Data.DataModels.PinglistModels;
using FRTools.Web.Infrastructure;
using FRTools.Web.Models;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [RoutePrefix("pinglist")]
    public class PinglistController : BaseController
    {
        public PinglistController(DataContext dataContext, FRToolsLogger logger) : base(dataContext, logger)
        {
        }

        [Route(Name = "PinglistInfo")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("create", Name = "PinglistCreate")]
        public ActionResult Create()
        {
            return View(new CreatePinglistViewModel());
        }

        [Route("create", Name = "PinglistCreatePost")]
        [HttpPost]
        public ActionResult Create(CreatePinglistViewModel model)
        {
            var pinglist = DataContext.Pinglists.Add(new Pinglist());
            pinglist.GeneratedId = CodeHelpers.GenerateId(5, DataContext.Pinglists.Select(x => x.GeneratedId).ToList());
            pinglist.SecretKey = CodeHelpers.GenerateId();
            pinglist.Name = model.Name;
            pinglist.IsPublic = model.IsPublic;

            if (Request.IsAuthenticated)
            {
                int userId = HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>();
                pinglist.Creator = DataContext.Users.Find(userId);
            }
            else
                TempData["NewList"] = (pinglist.GeneratedId, pinglist.SecretKey);

            DataContext.SaveChanges();

            return RedirectToRoute("PinglistDirect", new { listId = pinglist.GeneratedId, secretKey = pinglist.SecretKey });
        }

        [Route("delete", Name = "PinglistDelete")]
        public ActionResult Delete(PinglistViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);
            if (list != null && IsOwner(list, model.SecretKey))
            {
                DataContext.PingListEntries.RemoveRange(list.Entries.ToList());
                DataContext.Pinglists.Remove(list);
                DataContext.SaveChanges();

                AddSuccessNotification("The pinglist has been succesfully removed.");
            }
            else
            {
                AddErrorNotification("Only the owner can manage a pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("PinglistDirect", new { listId = model.ListId });
            }

            return RedirectToRoute("Pinglist");
        }

        [Route("list", Name = "Pinglist")]
        public ActionResult List()
        {
            var model = new PinglistListsViewModel();
            if (Request.IsAuthenticated)
            {
                var currentUser = DataContext.Users.Find(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>());
                var ownedLists = DataContext.Pinglists.Where(x => x.Creator.Id == currentUser.Id);
                model.OwnedLists = ownedLists.Select(x => new PinglistViewModel { Name = x.Name, ListId = x.GeneratedId, IsPublic = x.IsPublic, PinglistCategory = x.PinglistCategory }).ToList();
                model.AvailableCategories.AddRange(DataContext.PinglistCategories.Where(x => x.Owner.Id == currentUser.Id));

                if (currentUser.FRUser != null)
                {
                    var onLists = DataContext.Pinglists.Where(x => x.Entries.Any(e => e.FRUser.Id == currentUser.FRUser.Id));

                    model.OnLists = onLists.Select(x => new PinglistViewModel { Name = x.Name, ListId = x.GeneratedId, IsPublic = x.IsPublic, Owner = x.Creator, PinglistCategory = x.PinglistCategory }).ToList();
                    model.HasVerified = true;
                }
                else
                    model.HasVerified = false;
            }

            return View(model);
        }

        [Route("list/{listId}", Name = "PinglistDirect")]
        public ActionResult List(string listId, string secretKey = null)
        {
            var list = PinglistHelpers.GetPinglist(listId, true, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{listId}'.");
                return RedirectToRoute("PinglistInfo");
            }

            var model = new PinglistViewModel
            {
                Name = list.Name,
                Owner = list.Creator,
                ListId = list.GeneratedId,
                IsPublic = list.IsPublic
            };

            if (Request.IsAuthenticated)
                model.CurrentFRUser = DataContext.Users.Find(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>()).FRUser;

            if (!HasAccess(list, secretKey))
            {
                if (list.Entries.Any(x => x.FRUser.FRId == model.CurrentFRUser?.FRId))
                {
                    AddInfoNotification("This pinglist is private, however since you are on it you can manage your entry.");
                    var entry = list.Entries.FirstOrDefault(x => x.FRUser.Id == model.CurrentFRUser.Id);
                    model.EntriesViewModel = new PinglistEntriesViewModel
                    {
                        IsPublic = model.IsPublic,
                        CurrentUserId = entry.FRUser.User.Id,
                        CurrentFRUserId = entry.FRUser.FRId,
                        ListId = list.GeneratedId,
                        PinglistEntries = new[] { new PinglistEntryViewModel { EntryId = entry.GeneratedId, SecretKey = entry.SecretKey, FRUser = model.CurrentFRUser } }.ToList()
                    };
                    return View("PrivateViewList", model);
                }
                else
                {
                    AddErrorNotification("You do not have access to this pinglist. Make sure you are logged in or provide the correct secret key.");
                    return RedirectToRoute("Pinglist");
                }
            }
            else
                model.EntriesViewModel = new PinglistEntriesViewModel
                {
                    ListId = list.GeneratedId,
                    PinglistEntries = list.Entries.Select(x => new PinglistEntryViewModel { EntryId = x.GeneratedId, SecretKey = x.SecretKey, FRUser = x.FRUser, Remarks = x.Remarks }).ToList(),
                    IsPublic = model.IsPublic,
                    CurrentUserId = model.CurrentFRUser?.User.Id,
                    CurrentFRUserId = model.CurrentFRUser?.FRId
                };
            if (IsOwner(list, secretKey))
            {
                var ownerModel = new EditPinglistViewModel { Name = model.Name, Owner = model.Owner, ListId = model.ListId, CurrentFRUser = model.CurrentFRUser, EntriesViewModel = model.EntriesViewModel, IsPublic = list.IsPublic };
                ownerModel.EntriesViewModel.SecretKey = ownerModel.SecretKey = list.SecretKey;
                ownerModel.Format = list.Format == null ? new EditPinglistViewModel.FormatModel() : JsonConvert.DeserializeObject<EditPinglistViewModel.FormatModel>(list.Format);
                ownerModel.CopyPinglist = $"{ownerModel.Format.Prefix}{string.Join(ownerModel.Format.Separator, ownerModel.EntriesViewModel.PinglistEntries.Select(x => $"@{x.FRUser.Username}"))}{ownerModel.Format.Postfix}";
                ownerModel.AvailableCategories.Add(new PinglistCategory { Id = -1 });
                ownerModel.AvailableCategories.AddRange(DataContext.PinglistCategories.Where(x => x.Owner.Id == ownerModel.Owner.Id).ToList());
                ownerModel.PinglistCategory = list.PinglistCategory;
                ownerModel.ShareUrl = list.ShareUrl;
                ownerModel.FinishedJobs = JobManager.GetUnconfirmedFinishedJobs(model.ListId);
                var activeJobs = JobManager.GetActiveJobs(list.GeneratedId.ToString());
                foreach (var job in activeJobs)
                {
                    if (!string.IsNullOrEmpty(TempData["Info"] as string))
                        TempData["Info"] += "<br/>";
                    TempData["Info"] += $"Still working on: {job.Description}";
                }
                return View("OwnerViewList", ownerModel);
            }

            return View("PublicViewList", model);
        }

        [Route("list/{listId}/{jobId}", Name = "MarkPinglistJobRead")]
        public ActionResult MarkPinglistJobTaskRead(string listId, string secretKey, Guid jobId)
        {
            var list = PinglistHelpers.GetPinglist(listId, true, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{listId}'.");
                return RedirectToRoute("PinglistInfo");
            }

            var model = new PinglistViewModel
            {
                Name = list.Name,
                Owner = list.Creator,
                ListId = list.GeneratedId,
                IsPublic = list.IsPublic
            };

            if (Request.IsAuthenticated)
                model.CurrentFRUser = DataContext.Users.Find(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>()).FRUser;

            if (!HasAccess(list, secretKey))
            {
                AddErrorNotification("You do not have access to this pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("Pinglist");
            }

            JobManager.MarkFinishedJobRead(jobId);

            return RedirectToRoute("PinglistDirect", new { listId = model.ListId });
        }

        [Route("list/{listId}/addEntry", Name = "PinglistAddEntryPost")]
        [HttpPost]
        public async Task<ActionResult> AddEntry(AddEntryViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("PinglistInfo");
            }

            if (!HasAccess(list, model.SecretKey))
            {
                AddErrorNotification("You do not have access to this pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("Pinglist");
            }

            try
            {
                var entry = await PinglistHelpers.AddEntryToList(list, model.Username, model.UserId, model.Remarks, DataContext);

                if (!string.IsNullOrEmpty(TempData["Success"] as string))
                    TempData["Success"] += "<br/>";
                TempData["Success"] += $"User '{entry.FRUser.Username}' has been added to the pinglist.";

                if (!IsOwner(list, model.SecretKey))
                    TempData["NewEntry"] = (entry.GeneratedId, entry.SecretKey, entry.FRUser.Username);
            }
            catch
            {
                AddErrorNotification($"Could not find user '{(model.UserId?.ToString() ?? model.Username)}' on Flight Rising. Verify the name or id is correct.");
            }
            DataContext.SaveChanges();

            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("list/{listId}/addSelf", Name = "PinglistAddSelfPost")]
        [Authorize]
        [HttpPost]
        public async Task<ActionResult> AddSelf(AddSelfEntryViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("PinglistLink");
            }

            if (!HasAccess(list, model.SecretKey))
            {
                AddErrorNotification("You do not have access to this pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("PinglistDirect", new { listId = model.ListId });
            }

            var currentUser = DataContext.Users.Find(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>());

            if (currentUser.FRUser == null)
            {
                AddErrorNotification($"To use this feature you need to link your Flight Rising account, you can do so <a href=\"{Url.RouteUrl("VerifyFR")}\">here</a>.");
                return RedirectToRoute("PinglistDirect");
            }

            try
            {
                var entry = await PinglistHelpers.AddEntryToList(list, null, currentUser.FRUser.FRId, model.Remarks, DataContext);
                AddSuccessNotification($"User '{entry.FRUser.Username}' has been added to the pinglist.");
            }
            catch
            {
                AddErrorNotification($"Could not validate the existence of user '{currentUser.FRUser.FRId}.'");
            }
            DataContext.SaveChanges();

            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("list/{listId}/removeEntry/{entryId}/{entrySecret}")]
        public ActionResult RemoveEntry(RemoveEntryViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("PinglistLink");
            }

            var entry = list.Entries.FirstOrDefault(x => x.GeneratedId == model.EntryId);
            var currentUser = DataContext.Users.Find(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>());

            if (entry == null || (entry.SecretKey != model.EntrySecret && (currentUser.FRUser == null || entry.FRUser.Id != currentUser.FRUser.Id)))
            {
                AddErrorNotification($"Could not find entry id '{model.EntryId}' in list '{list.Name}'.");
                return RedirectToRoute("PinglistDirect", new { model.ListId });
            }

            list.Entries.Remove(entry);
            AddSuccessNotification($"The entry for user '{entry.FRUser.Username}' has been removed.");
            DataContext.SaveChanges();

            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("link", Name = "PinglistLink")]
        [Authorize]
        public ActionResult LinkExisting()
        {
            return View(new PinglistViewModel());
        }

        [Route("link", Name = "PinglistLinkPost")]
        [HttpPost]
        [Authorize]
        public ActionResult LinkExisting(PinglistViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("PinglistLink");
            }

            if (list.Creator != null)
            {
                AddErrorNotification("This list is already linked to an account");
                return RedirectToRoute("PinglistLink");
            }

            if (list.SecretKey != model.SecretKey)
            {
                AddErrorNotification("Secret key does not match");
                return RedirectToRoute("PinglistLink");
            }

            int userId = HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>();

            list.Creator = DataContext.Users.Find(userId);
            AddSuccessNotification("Pinglist has been succesfully linked to your account");

            DataContext.SaveChanges();

            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("entry/manage", Name = "PinglistEntryManage")]
        [HttpPost]
        public ActionResult ManageEntry(ManagePinglistEntryViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("PinglistLink");
            }

            var entry = list.Entries.FirstOrDefault(x => x.GeneratedId == model.EntryViewModel.EntryId);

            if (entry == null || entry.SecretKey != model.EntryViewModel.SecretKey)
            {
                AddErrorNotification($"Could not find entry id '{model.EntryViewModel.EntryId}' in list '{list.Name}'");
                return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
            }

            entry.Remarks = model.EntryViewModel.Remarks;
            DataContext.SaveChanges();

            AddInfoNotification($"Entry '{entry.GeneratedId} ' updated.");
            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("manage/{listId}/{secretKey}", Name = "PinglistManageListPost")]
        [HttpPost]
        public ActionResult ManageList(EditPinglistViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("Pinglist");
            }

            if (!HasAccess(list, model.SecretKey))
            {
                AddErrorNotification("You do not have access to this pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("Pinglist");
            }

            if (!IsOwner(list, model.SecretKey))
            {
                AddErrorNotification("Only the owner can manage a pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("PinglistDirect", new { model.ListId });
            }

            list.Name = model.Name;
            list.IsPublic = model.IsPublic;
            list.PinglistCategory = Request.IsAuthenticated && model.NewPinglistCategory.HasValue && list.Creator != null ? DataContext.PinglistCategories.FirstOrDefault(x => x.Owner.Id == list.Creator.Id && x.Id == model.NewPinglistCategory.Value) : null;
            list.Format = JsonConvert.SerializeObject(model.Format);

            DataContext.SaveChanges();

            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("manage/{listId}/{secretKey}/import", Name = "PinglistImportCSV")]
        [HttpPost]
        public ActionResult ImportPinglist(ImportPingsViewModel model)
        {
            int listId;
            var list = PinglistHelpers.GetPinglist(model.ListId, false, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("Pinglist");
            }

            if (!HasAccess(list, model.SecretKey))
            {
                AddErrorNotification("You do not have access to this pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("Pinglist");
            }

            if (!string.IsNullOrWhiteSpace(model.CSV))
            {
                var jobResult = JobManager.StartNewJob(new ImportCSVPinglist(model.ListId, model.CSV));
                AddInfoNotification($"Your pinglist is being imported in the background, depending on the size this can take a while. You started this job at '{jobResult.StartTime}'.");
            }

            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("manage/{listId}/{secretKey}/updateusers", Name = "PinglistBatchUpdateUsers")]
        public ActionResult UpdatePinglistUsers(PinglistViewModel model)
        {
            var list = PinglistHelpers.GetPinglist(model.ListId, true, DataContext);

            if (list == null)
            {
                AddErrorNotification($"There is no pinglist with the ID '{model.ListId}'.");
                return RedirectToRoute("PinglistDirect");
            }

            if (!HasAccess(list, model.SecretKey))
            {
                AddErrorNotification("You do not have access to this pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("PinglistDirect", new { model.ListId });
            }

            if (!IsOwner(list, model.SecretKey))
            {
                AddErrorNotification("Only the owner can manage a pinglist. Make sure you are logged in or provide the correct secret key.");
                return RedirectToRoute("PinglistDirect", new { model.ListId });
            }

            var job = Task.Run(async () =>
            {
                foreach (var entry in list.Entries.Select(x => x.FRUser.FRId).ToList())
                    await FRHelpers.GetOrUpdateFRUser(entry);
            });
            var jobResult = JobManager.StartNewJob(new UpdatePinglist(list.GeneratedId, list.Entries.Select(x => x.FRUser.FRId).ToList()));
            AddInfoNotification($"The entries on your pinglist are being updated in the background, depending on the size this can take a while. You started this job at '{jobResult.StartTime}'.");

            return RedirectToRoute("PinglistDirect", new { model.ListId, model.SecretKey });
        }

        [Route("manage/category/create", Name = "PinglistCategoryAdd")]
        [HttpPost]
        [Authorize]
        public ActionResult AddCategory(string name)
        {
            var user = DataContext.Users.Find(HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>());
            var cat = DataContext.PinglistCategories.Add(new PinglistCategory { Name = name, Owner = user });
            DataContext.SaveChanges();
            return Json(new { Result = 1, cat.Id, cat.Name });
        }

        [Route("manage/category/edit", Name = "PinglistCategoryEdit")]
        [HttpPost]
        [Authorize]
        public ActionResult EditCategory(int id, string name)
        {

            var cat = DataContext.PinglistCategories.Include(x => x.Owner).FirstOrDefault(x => x.Id == id);
            if (cat.Owner.Id == HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>())
            {
                cat.Name = name;
                DataContext.SaveChanges();
                return Json(new { cat.Id, cat.Name });
            }
            else
                return Json(new { Result = -1 });
        }

        [Route("manage/category/delete", Name = "PinglistCategoryDelete")]
        [HttpPost]
        [Authorize]
        public ActionResult DeleteCategory(int id)
        {

            var cat = DataContext.PinglistCategories.Include(x => x.Owner).Include(x => x.Pinglists).FirstOrDefault(x => x.Id == id);
            if (cat.Owner.Id == HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>())
            {
                foreach (var pinglist in cat.Pinglists)
                    pinglist.PinglistCategory = null;
                DataContext.PinglistCategories.Remove(cat);
                DataContext.SaveChanges();
            }
            return Json(new { Result = -1 });
        }

        private bool HasAccess(Pinglist list, string secretKey)
        {
            if (list.IsPublic || (list.SecretKey == secretKey && list.Creator == null) || (list.Creator != null && list.Creator.Id == HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>()))
                return true;

            return false;
        }

        private bool IsOwner(Pinglist list, string secretKey)
        {
            if (list.Creator == null && list.SecretKey == secretKey)
                return true;
            if (list.Creator != null && Request.IsAuthenticated && list.Creator.Id == HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>())
                return true;

            return false;
        }
    }
}