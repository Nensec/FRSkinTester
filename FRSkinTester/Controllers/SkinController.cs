﻿using FRTools.Infrastructure;
using FRTools.Infrastructure.DataModels;
using FRTools.Models;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FRTools.Controllers
{
    [RoutePrefix("skintester")]
    public class SkinController : BaseController
    {
        [Route("preview", Name = "PreviewHome")]
        public ActionResult PreviewHome()
        {
            return View();
        }

        [Route("preview/{skinId}", Name = "Preview")]
        public async Task<ActionResult> Preview(PreviewModelGet model)
        {
            using (var ctx = new DataContext())
            {
                var skin = ctx.Skins.FirstOrDefault(x => x.GeneratedId == model.SkinId);
                if (skin == null)
                {
                    TempData["Error"] = "Skin not found";
                    return RedirectToRoute("Home");
                }
                if (skin.Coverage == null)
                    await UpdateCoverage(skin, ctx);
                try
                {
                    return View(new PreviewModelViewModel
                    {
                        Title = skin.Title,
                        Description = skin.Description,
                        SkinId = model.SkinId,
                        PreviewUrl = (await GenerateOrFetchPreview(model.SkinId, "preview", string.Format(DressingRoomDummyUrl, skin.DragonType, skin.GenderType), null)).Urls[0],
                        Coverage = skin.Coverage,
                        Creator = skin.Creator,
                        DragonType = (DragonType)skin.DragonType,
                        Gender = (Gender)skin.GenderType
                    });
                }
                catch (FileNotFoundException)
                {
                    TempData["Error"] = "Skin not found";
                    return RedirectToRoute("Home");
                }
            }
        }

        [HttpPost]
        [Route("preview/{skinId}", Name = "PreviewPost")]
        public async Task<ActionResult> Preview(PreviewModelPost model)
        {
            if (!ModelState.IsValid)
                return RedirectToRoute("Preview", new { model.SkinId });

            if (model.DragonId != null)
            {
                string dwagonUrl = null;
                using (var client = new WebClient())
                {
                    var htmlPage = await client.DownloadStringTaskAsync(new Uri(string.Format(ScryerUrl, model.DragonId)));
                    dwagonUrl = ScrapeImageUrl(htmlPage);
                }

                if (dwagonUrl.StartsWith(".."))
                {
                    TempData["Error"] = $"<b>{model.DragonId}</b> appears to be an invalid dragon id";
                    return RedirectToRoute("Preview", new { model.SkinId });
                }

                return await GeneratePreview(model.SkinId, dwagonUrl, model.DragonId, model.Force);

            }
            else if (!string.IsNullOrWhiteSpace(model.ScryerUrl))
            {
                return await GeneratePreview(model.SkinId, model.ScryerUrl);
            }
            else if (!string.IsNullOrWhiteSpace(model.DressingRoomUrl))
            {
                return await GeneratePreview(model.SkinId, model.DressingRoomUrl, null, model.Force);
            }

            return RedirectToRoute("Preview", new { model.SkinId });
        }

        private async Task<ActionResult> GeneratePreview(string skinId, string dragonUrl, int? dragonId = null, bool force = false)
        {
            DragonCache dragon = null;
            bool isDressingRoomUrl = dragonUrl.Contains("/dgen/dressing-room");
            string dressingRoomUrl = isDressingRoomUrl ? dragonUrl : null;
            if (isDressingRoomUrl)
            {
                var apparelDragon = ParseUrlForDragon(dragonUrl);
                if (!dragonUrl.Contains("/dgen/dressing-room/dummy"))
                {
                    dragonId = int.Parse(Regex.Match(dragonUrl, @"did=([\d]*)").Groups[1].Value);
                    using (var client = new WebClient())
                    {
                        var htmlPage = await client.DownloadStringTaskAsync(new Uri(string.Format(ScryerUrl, dragonId)));
                        dragonUrl = ScrapeImageUrl(htmlPage);
                    }
                }
                dragon = ParseUrlForDragon(dragonUrl);
                if (IsAncientBreed(dragon.DragonType))
                {
                    TempData["Error"] = $"Ancient breeds cannot wear apparal, how did you even get a dressing room link in here?";
                    return RedirectToRoute("Preview", new { skinId });
                }
                dragon.Apparel = apparelDragon.Apparel;
                if (dragon.GetApparel().Length == 0)
                {
                    TempData["Error"] = $"This dressing room URL contains no apparel";
                    return RedirectToRoute("Preview", new { skinId });
                }
            }
            else
                dragon = ParseUrlForDragon(dragonUrl);

            if (dragon.Age == Age.Hatchling)
            {
                TempData["Error"] = $"Skins can only be previewed on adult dragons";
                return RedirectToRoute("Preview", new { skinId });
            }

            using (var ctx = new DataContext())
            {
                var skin = ctx.Skins.FirstOrDefault(x => x.GeneratedId == skinId);
                if (skin == null)
                {
                    TempData["Error"] = "Skin not found";
                    return RedirectToRoute("Home");
                }

                if (skin.DragonType != (int)dragon.DragonType)
                {
                    TempData["Error"] = $"This skin is meant for a <b>{(DragonType)skin.DragonType} {(Gender)skin.GenderType}</b>, the dragon you provided is a <b>{dragon.DragonType} {dragon.Gender}</b>";
                    return RedirectToRoute("Preview", new { skinId });
                }

                if (skin.GenderType != (int)dragon.Gender)
                {
                    TempData["Error"] = $"This skin is meant for a <b>{(Gender)skin.GenderType}</b>, the dragon you provided is a <b>{dragon.Gender}</b>";
                    return RedirectToRoute("Preview", new { skinId });
                }

                var previewResult = await GenerateOrFetchPreview(skinId, dragonId?.ToString(), dragonUrl, dragon, dressingRoomUrl, force);

                var loggedInUserId = HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>();

                var user = ctx.Users.Find(loggedInUserId);
                foreach (var url in previewResult.Urls.Where(url => !skin.Previews.Any(x => x.Requestor == user && x.PreviewImage == url)))
                {
                    skin.Previews.Add(new Preview
                    {
                        DragonId = dragonId,
                        ScryerUrl = dragonId == null ? dragonUrl : null,
                        PreviewImage = url,
                        DragonData = dragon.ToString(),
                        PreviewTime = DateTime.UtcNow,
                        Requestor = ctx.Users.Find(loggedInUserId)
                    });
                }

                await ctx.SaveChangesAsync();

                return View("PreviewResult", new PreviewModelPostViewModel
                {
                    SkinId = skinId,
                    Result = previewResult,
                    Dragon = dragon
                });
            }
        }

        [Route("upload", Name = "Upload")]
        public ActionResult Upload() => View(new UploadModelPost());

        [Route("upload", Name = "UploadPost")]
        [HttpPost]
        public async Task<ActionResult> Upload(UploadModelPost model)
        {
            var azureImageService = new AzureImageService();

            using (var ctx = new DataContext())
            {
                var randomizedId = GenerateId(5, ctx.Skins.Select(x => x.GeneratedId).ToList());
                var secretKey = GenerateId(7);
                Bitmap skinImage = null;
                try
                {
                    skinImage = (Bitmap)Image.FromStream(model.Skin.InputStream);
                    if (skinImage.Width != 350 || skinImage.Height != 350)
                    {
                        TempData["Error"] = "Image needs to be 350px x 350px. Just like FR.";
                        return View();
                    }

                }
                catch
                {
                    TempData["Error"] = "Upload is not a valid png image";
                    return View();
                }
                try
                {
                    model.Skin.InputStream.Position = 0;
                    var url = await azureImageService.WriteImage($@"skins\{randomizedId}.png", model.Skin.InputStream);

                    Bitmap dragonImage = null;
                    using (var client = new WebClient())
                    {
                        var dwagonImageBytes = client.DownloadDataTaskAsync(string.Format(DressingRoomDummyUrl, (int)model.DragonType, (int)model.Gender));
                        try
                        {
                            using (var memStream = new MemoryStream(await dwagonImageBytes, false))
                                dragonImage = (Bitmap)Image.FromStream(memStream);
                        }
                        catch
                        {
                        }
                    }

                    var skin = new Skin
                    {
                        GeneratedId = randomizedId,
                        SecretKey = secretKey,
                        Title = model.Title,
                        Description = model.Description,
                        DragonType = (int)model.DragonType,
                        GenderType = (int)model.Gender,
                        Coverage = GetCoveragePercentage(skinImage, dragonImage)
                    };
                    skinImage.Dispose();

                    if (Request.IsAuthenticated)
                    {
                        var userId = HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>();
                        skin.Creator = ctx.Users.FirstOrDefault(x => x.Id == userId);
                    }

                    ctx.Skins.Add(skin);
                    await ctx.SaveChangesAsync();
                    return View("UploadResult", new UploadModelPostViewModel
                    {
                        SkinId = randomizedId,
                        SecretKey = secretKey,
                        PreviewUrl = (await GenerateOrFetchPreview(randomizedId, "preview", string.Format(DressingRoomDummyUrl, skin.DragonType, skin.GenderType), null)).Urls[0],
                    });
                }
                catch
                {
                    TempData["Error"] = "Something went wrong uploading";
                    return View();
                }
            }
        }

        [Route("manage/skin/{skinId}/{secretKey}", Name = "Manage")]
        public async Task<ActionResult> Manage(ManageModelGet model)
        {
            using (var ctx = new DataContext())
            {
                var skin = ctx.Skins.Include(x => x.Previews).FirstOrDefault(x => x.GeneratedId == model.SkinId && x.SecretKey == model.SecretKey);
                if (skin == null)
                {
                    TempData["Error"] = "Skin not found or secret invalid";
                    return RedirectToRoute("Home");
                }
                else
                {
                    if (skin.Creator != null)
                    {
                        if (!Request.IsAuthenticated)
                        {
                            TempData["Error"] = "This skin is linked to an acocunt, please log in to manage this skin.";
                            return RedirectToRoute("Home");
                        }
                        else if (skin.Creator.Id != HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>())
                        {
                            TempData["Error"] = "This skin is linked to a different account than the one that is logged in, you can only manage your own skins.";
                            return RedirectToRoute("Home");
                        }
                    }

                    if (skin.Coverage == null)
                        await UpdateCoverage(skin, ctx);
                    return View(new ManageModelViewModel
                    {
                        Skin = skin,
                        PreviewUrl = (await GenerateOrFetchPreview(model.SkinId, "preview", string.Format(DressingRoomDummyUrl, skin.DragonType, skin.GenderType), null)).Urls[0]
                    });
                }
            }
        }

        [Route("manage/skin", Name = "ManagePost")]
        [HttpPost]
        public async Task<ActionResult> Manage(ManageModelPost model)
        {
            using (var ctx = new DataContext())
            {
                var skin = ctx.Skins.FirstOrDefault(x => x.GeneratedId == model.SkinId && x.SecretKey == model.SecretKey);
                if (skin == null)
                {
                    TempData["Error"] = "Skin not found or secret invalid";
                    return RedirectToRoute("Home");
                }
                else
                {
                    if (skin.Creator != null)
                    {
                        if (!Request.IsAuthenticated)
                        {
                            TempData["Error"] = "This skin is linked to an acocunt, please log in to manage this skin.";
                            return RedirectToRoute("Home");
                        }
                        else if (skin.Creator.Id != HttpContext.GetOwinContext().Authentication.User.Identity.GetUserId<int>())
                        {
                            TempData["Error"] = "This skin is linked to a different account than the one that is logged in, you can only manage your own or unclaimed skins.";
                            return RedirectToRoute("Home");
                        }
                    }

                    if (skin.GenderType != (int)model.Gender || skin.DragonType != (int)model.DragonType)
                    {
                        var azureImageService = new AzureImageService();
                        await azureImageService.DeleteImage($@"previews\{model.SkinId}\preview.png");
                    }

                    skin.Title = model.Title;
                    skin.Description = model.Description;
                    skin.GenderType = (int)model.Gender;
                    skin.DragonType = (int)model.DragonType;

                    await ctx.SaveChangesAsync();

                    TempData["Info"] = "Changes have been saved!";
                    return RedirectToRoute("Manage", new { model.SkinId, model.SecretKey });
                }
            }
        }

        [Route("delete", Name = "Delete")]
        public async Task<ActionResult> Delete(DeleteSkinPost model)
        {
            using (var ctx = new DataContext())
            {
                var skin = ctx.Skins.FirstOrDefault(x => x.GeneratedId == model.SkinId && x.SecretKey == model.SecretKey);
                if (skin == null)
                {
                    TempData["Error"] = "Skin not found or secret invalid";
                    return RedirectToRoute("Home");
                }
                else
                {
                    var azureImageService = new AzureImageService();
                    await azureImageService.DeleteImage($@"skins\{model.SkinId}.png");
                    skin.Previews.Clear();
                    ctx.Skins.Remove(skin);
                    await ctx.SaveChangesAsync();
                }
            }
            return View();
        }

        static Dictionary<(int, int), byte[]> _dummyCache = new Dictionary<(int, int), byte[]>();

        public async Task<ActionResult> GetDummyDragon(int dragonType, int gender)
        {
            if (!_dummyCache.TryGetValue((dragonType, gender), out var bytes))
            {
                var dummyDragon = await GetDragonBaseImage(string.Format(DressingRoomDummyUrl, dragonType, gender), null, new AzureImageService());
                using (var memStream = new MemoryStream())
                {
                    dummyDragon.Save(memStream, ImageFormat.Png);
                    _dummyCache[(dragonType, gender)] = bytes = memStream.ToArray();
                }
            }

            return File(bytes, "image/png");
        }

        [Route("")]
        [Route("browse", Name = "Browse")]
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public async Task<ActionResult> Browse(BrowseFilterModel filter)
        {
            var model = new BrowseViewModel { Filter = filter };
            using (var ctx = new DataContext())
            {
                var query = ctx.Skins                    
                    .Where(x => filter.DragonTypes.Contains((DragonType)x.DragonType))
                    .Where(x => filter.Genders.Contains((Gender)x.GenderType))
                    .Where(x => filter.SkinTypes.Contains((BrowseFilterModel.SkinType)(x.Coverage >= 31 ? 1 : 0)));

                if (!string.IsNullOrEmpty(filter.Name))
                    query = query.Where(x => x.Title.Contains(filter.Name));

                model.TotalResults = query.Count();

                query = query.OrderByDescending(x => x.Id).Skip(filter.PageAmount * (filter.Page - 1)).Take(filter.PageAmount);

                model.Results = query.Select(x => new PreviewModelViewModel
                {
                    Title = x.Title,
                    Description = x.Description,
                    SkinId = x.GeneratedId,
                    Coverage = x.Coverage,
                    Creator = x.Creator,
                    DragonType = (DragonType)x.DragonType,
                    Gender = (Gender)x.GenderType
                }).ToList();

                foreach (var result in model.Results)
                    result.PreviewUrl = (await GenerateOrFetchPreview(result.SkinId, "preview", string.Format(DressingRoomDummyUrl, (int)result.DragonType, (int)result.Gender), null)).Urls[0];
            }

            return View(model);
        }
    }
}