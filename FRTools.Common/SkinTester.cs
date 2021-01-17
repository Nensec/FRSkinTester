using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Data.DataModels.FlightRisingModels;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FRTools.Common
{
    public class SkinTester
    {
        private readonly FRToolsLogger _logger;
        private readonly DataContext _dataContext;

        public SkinTester(FRToolsLogger logger, DataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task<PreviewResult> GenerateOrFetchPreview(string skinId, int dragonId, bool swapSilhouette = false, bool force = false, int? version = null)
        {
            _logger.Log(LogItemOrigin.SkinTester, LogItemSeverity.Debug, $"GenerateOrFetchPreview called SkinId:{skinId}, DragonId:{dragonId}, Swap:{swapSilhouette}, Force:{force}, Version:{version}");
            var result = new PreviewResult(PreviewSource.DragonId) { Forced = force };
            var dragonUrl = FRHelpers.GetDragonImageUrlFromDragonId(dragonId);
            if (dragonUrl.StartsWith(".."))
                return result.WithErrorMessage("{0} appears to be an invalid dragon id", dragonId);

            var dragon = FRHelpers.ParseUrlForDragon(dragonUrl);
            dragon.FRDragonId = dragonId;
            return await GenerateOrFetchPreview(result, skinId, version, dragon, false, swapSilhouette, force);
        }

        public async Task<PreviewResult> GenerateOrFetchPreview(string skinId, string dragonUrl, bool force = false, int? version = null)
        {
            _logger.Log(LogItemOrigin.SkinTester, LogItemSeverity.Debug, $"GenerateOrFetchPreview called SkinId:{skinId}, DragonUrl:{dragonUrl}, Force:{force}, Version:{version}");

            var result = new PreviewResult(PreviewSource.DressingRoom);
            if (dragonUrl.Contains("/dgen/dressing-room"))
            {
                // Dressing room link
                result = new PreviewResult(PreviewSource.DressingRoom) { Forced = force };

                var apparelDragon = FRHelpers.ParseUrlForDragon(dragonUrl);
                DragonCache dragon;
                if (dragonUrl.Contains("/dgen/dressing-room/dummy"))
                    dragon = FRHelpers.ParseUrlForDragon(dragonUrl);
                else if (dragonUrl.Contains("dgen/dressing-room/scry"))
                {
                    var scryId = int.Parse(Regex.Match(dragonUrl, @"sdid=([\d]*)").Groups[1].Value);
                    var scryUrl = FRHelpers.GetDragonImageUrlFromScryId(scryId);
                    dragon = FRHelpers.ParseUrlForDragon(scryUrl);
                }
                else
                {
                    var dragonId = int.Parse(Regex.Match(dragonUrl, @"did=([\d]*)").Groups[1].Value);
                    dragon = FRHelpers.GetDragonFromDragonId(dragonId);
                }

                if (FRHelpers.IsAncientBreed(dragon.DragonType))
                    return result.WithErrorMessage("Ancient breeds cannot wear apparal.");

                dragon.Apparel = apparelDragon.Apparel;
                if (dragon.GetApparel().Length == 0)
                    return result.WithErrorMessage("This dressing room URL contains no apparel.");

                result = await GenerateOrFetchPreview(result, skinId, version, dragon, true, false, force);
            }
            else if (dragonUrl.Contains("dgen/preview/dragon"))
            {
                // Scry image link
                result = new PreviewResult(PreviewSource.Scry) { Forced = force };

                var dragon = FRHelpers.ParseUrlForDragon(dragonUrl);
                result = await GenerateOrFetchPreview(result, skinId, version, dragon, false, false, force);
            }
            else
                return result.WithErrorMessage("The URL provided is not valid.");

            result.DragonUrl = dragonUrl;
            return result;
        }

        public async Task<PreviewResult> GenerateOrFetchDummyPreview(string skinId, int version) => await GenerateOrFetchPreview(new PreviewResult(PreviewSource.Dummy), skinId, version, null, false, false, false);

        private async Task<PreviewResult> GenerateOrFetchPreview(PreviewResult result, string skinId, int? version, DragonCache dragon, bool isDressingRoom, bool swapSilhouette, bool force)
        {
            Skin skin;
            var skins = _dataContext.Skins.Where(x => x.GeneratedId == skinId);
            if (version == null)
                skin = skins.OrderByDescending(x => x.Version).FirstOrDefault();
            else
                skin = skins.FirstOrDefault(x => x.Version == version);

            if (skin == null)
                return result.WithErrorMessage("Skin not found.");

            result.Skin = skin;

            if (dragon == null)
                dragon = FRHelpers.ParseUrlForDragon(string.Format(FRHelpers.DressingRoomDummyUrl, skin.DragonType, skin.GenderType), skinId, version);

            result.Dragon = dragon;

            if (dragon.Age == Age.Hatchling)
            {
                return result.WithErrorMessage("Skins can only be previewed on adult dragons.");
            }

            if (swapSilhouette)
            {
                var swappedDragon = FRHelpers.ParseUrlForDragon(FRHelpers.GenerateDragonImageUrl(dragon, swapSilhouette));
                swappedDragon.FRDragonId = dragon.FRDragonId;
                dragon = swappedDragon;
            }

            if (skin.DragonType != (int)dragon.DragonType)
                return result.WithErrorMessage("This skin is meant for a {0} {1}, the dragon you provided is a {2} {3}.", (DragonType)skin.DragonType, (Gender)skin.GenderType, dragon.DragonType, dragon.Gender);

            if (skin.GenderType != (int)dragon.Gender)
                return result.WithErrorMessage("This skin is meant for a {0}, the dragon you provided is a {1}.", (Gender)skin.GenderType, dragon.Gender);

            Bitmap dragonImage = null;

            var azureImagePreviewPath = $@"previews\{skinId}\{(version == 1 ? "" : $@"{version}\")}{dragon.FRDragonId?.ToString() ?? dragon.ToString()}_{dragon.Gender}.png";
            dragon.PreviewUrls.TryGetValue(skinId, out var previewUrl);

            if (force || (previewUrl == null && !new AzureImageService().Exists(azureImagePreviewPath, out previewUrl)))
            {
                try
                {
                    dragonImage = await FRHelpers.GetDragonBaseImage(dragon);
                }
                catch (Exception ex)
                {
                    return result.WithErrorMessage(ex.ToString());
                }

                Image skinImage;
                using (var skinImageStream = await new AzureImageService().GetImage($@"skins\{skinId}.png"))
                    skinImage = Image.FromStream(skinImageStream);

                var fixPixelFormat = FixPixelFormat((Bitmap)skinImage);
                if (fixPixelFormat != null)
                {
                    skinImage = fixPixelFormat;
                    using (var memStream = new MemoryStream())
                    {
                        skinImage.Save(memStream, ImageFormat.Png);
                        memStream.Position = 0;

                        _ = await new AzureImageService().WriteImage($@"skins\{skinId}.png", memStream);
                    }
                }

                var eyeMask = (Bitmap)Image.FromFile(CodeHelpers.MapPath($@"\Masks\{(int)dragon.DragonType}_{(int)dragon.Gender}_{(dragon.EyeType == EyeType.Primal || dragon.EyeType == EyeType.MultiGaze ? (int)dragon.EyeType : 0)}_{(dragon.EyeType == EyeType.Primal ? (int)dragon.Element : 0)}.png"));

                if (dragon.EyeType == EyeType.MultiGaze)
                {
                    using (var graphics = Graphics.FromImage(eyeMask))
                    {
                        var normaleye = Image.FromFile(CodeHelpers.MapPath($@"\Masks\{(int)dragon.DragonType}_{(int)dragon.Gender}_0_0.png"));
                        graphics.DrawImage(normaleye, new Rectangle(0, 0, 350, 350));
                        graphics.Save();
                    }
                }

                for (int x = 0; x < eyeMask.Width; x++)
                {
                    for (int y = 0; y < eyeMask.Height; y++)
                    {
                        var pixel = eyeMask.GetPixel(x, y);
                        if (pixel.A > 0)
                        {
                            var skinPixel = ((Bitmap)skinImage).GetPixel(x, y);
                            if (skinPixel.A > 0)
                                ((Bitmap)skinImage).SetPixel(x, y, System.Drawing.Color.FromArgb(255 - pixel.A, skinPixel.R, skinPixel.G, skinPixel.B));
                        }
                    }
                }

                using (var graphics = Graphics.FromImage(dragonImage))
                {
                    graphics.DrawImage(skinImage, new Rectangle(0, 0, 350, 350));
                }

                using (var saveImageStream = new MemoryStream())
                {
                    dragonImage.Save(saveImageStream, ImageFormat.Png);
                    saveImageStream.Position = 0;

                    previewUrl = await new AzureImageService().WriteImage(azureImagePreviewPath, saveImageStream);

                    if (dragon.PreviewUrls.ContainsKey(skinId))
                        dragon.PreviewUrls[skinId] = previewUrl;
                    else
                        dragon.PreviewUrls.Add(skinId, previewUrl);
                }
            }
            else
            {
                result.Cached = true;
            }

            result.Urls = new[] { previewUrl };

            async Task<string> GenerateApparelPreview(Bitmap invisibleDragon, string cacheUrl)
            {
                if (dragonImage == null)
                    dragonImage = (Bitmap)Image.FromStream(await new AzureImageService().GetImage(azureImagePreviewPath));

                using (var graphics = Graphics.FromImage(dragonImage))
                {
                    graphics.DrawImage(invisibleDragon, new Rectangle(0, 0, 350, 350));
                }

                using (var saveApparelImageStream = new MemoryStream())
                {
                    dragonImage.Save(saveApparelImageStream, ImageFormat.Png);
                    saveApparelImageStream.Position = 0;

                    return await new AzureImageService().WriteImage(cacheUrl, saveApparelImageStream);
                }
            }

            dragon.PreviewUrls.TryGetValue(skinId + "apparel", out var apparelPreviewUrl);
            if (force || apparelPreviewUrl == null)
                if (isDressingRoom)
                {
                    var apparelIds = dragon.GetApparel();
                    var cacheUrl = $@"previews\{skinId}\{dragon.FRDragonId?.ToString() ?? dragon.ToString()}_{dragon.Gender}_apparel.png";
                    if (force || !new AzureImageService().Exists(cacheUrl, out apparelPreviewUrl))
                    {
                        string dressingRoomUrl;
                        if (dragon.FRDragonId.HasValue)
                            dressingRoomUrl = string.Format(FRHelpers.DressingRoomDragonApparalUrl, dragon.FRDragonId, string.Join(",", apparelIds.Concat(new[] { 22046 })));
                        else
                            dressingRoomUrl = string.Format(FRHelpers.DressingRoomDummyUrl, (int)dragon.DragonType, (int)dragon.Gender) + $"&apparel={string.Join(",", apparelIds.Concat(new[] { 22046 }))}";

                        var invisibleDragon = await FRHelpers.GetInvisibleDressingRoomDragon(dressingRoomUrl);
                        apparelPreviewUrl = await GenerateApparelPreview(invisibleDragon, cacheUrl);
                    }
                }
                else if (dragon.FRDragonId.HasValue && !FRHelpers.IsAncientBreed(dragon.DragonType))
                {
                    var cacheUrl = $@"previews\{skinId}\{dragon.FRDragonId}_{dragon.Gender}_apparel.png";
                    if (force || !new AzureImageService().Exists(cacheUrl, out apparelPreviewUrl))
                    {
                        var invisibleDragonWithApparel = await GetInvisibleDragonWithApparel(dragon, force);

                        if (invisibleDragonWithApparel != null)
                            apparelPreviewUrl = await GenerateApparelPreview(invisibleDragonWithApparel, cacheUrl);

                    }
                }
            if (apparelPreviewUrl != null)
            {
                if (dragon.PreviewUrls.ContainsKey(skinId + "apparel"))
                    dragon.PreviewUrls[skinId + "apparel"] = previewUrl;
                else
                    dragon.PreviewUrls.Add(skinId + "apparel", previewUrl);

                result.Urls = new[] { previewUrl, apparelPreviewUrl };
            }

            result.Success = true;
            return result;
        }

        public async Task<Bitmap> GetInvisibleDragonWithApparel(DragonCache dragon, bool force = false)
        {
            Bitmap invisibleDwagon;
            var azureUrl = $@"dragoncache\{dragon.FRDragonId}_{dragon.Gender}_invisible.png";
            if (!force && new AzureImageService().Exists(azureUrl, out var cacheUrl))
            {
                using (var stream = await new AzureImageService().GetImage(azureUrl))
                    invisibleDwagon = (Bitmap)Image.FromStream(stream);
                return invisibleDwagon;
            }

            using (var client = new WebClient())
            {
                var dragonProfilePageTask = client.DownloadStringTaskAsync(string.Format(FRHelpers.DragonProfileUrl, dragon.FRDragonId));
                try
                {
                    var dragonProfileHtml = await dragonProfilePageTask;
                    var apparel = Regex.Matches(dragonProfileHtml, @"<div class="".*dragon-profile-apparel-item.*"".+data-itemid=""(\d+)"".+>.+</div>");

                    if (apparel.Count == 0)
                        return null;

                    var invisibleDragonBytesTask = client.DownloadDataTaskAsync(string.Format(FRHelpers.DressingRoomDummyApparalUrl, (int)dragon.DragonType, (int)dragon.Gender, string.Join(",", apparel.Cast<Match>().Where(x => x.Success).Select(x => x.Groups[1].Value).Concat(new[] { "22046" }))));

                    var invisibleDwagonImageBytes = await invisibleDragonBytesTask;

                    using (var memStream = new MemoryStream(invisibleDwagonImageBytes, false))
                        await new AzureImageService().WriteImage(azureUrl, memStream);

                    using (var memStream = new MemoryStream(invisibleDwagonImageBytes, false))
                    {
                        invisibleDwagon = (Bitmap)Image.FromStream(memStream);
                        return invisibleDwagon;
                    }
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        public Bitmap FixPixelFormat(Bitmap skinImage)
        {
            if (skinImage.PixelFormat != PixelFormat.Format32bppArgb)
            {
                var clone = new Bitmap(skinImage.Width, skinImage.Height, PixelFormat.Format32bppArgb);
                using (var gr = Graphics.FromImage(clone))
                    gr.DrawImage(skinImage, new Rectangle(0, 0, clone.Width, clone.Height));

                skinImage.Dispose();
                return clone;
            }
            return skinImage;
        }
    }
}
