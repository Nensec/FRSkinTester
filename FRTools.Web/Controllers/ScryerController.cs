using FRTools.Common;
using FRTools.Data;
using FRTools.Web.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [RoutePrefix("scryer")]
    public class ScryerController : BaseController
    {
        public ScryerController(DataContext dataContext, FRToolsLogger logger) : base(dataContext, logger)
        {
        }

        [Route("dress", Name = "ScryerDresser")]
        public ActionResult Dress() => View();

        [Route("dress")]
        [HttpPost]
        public async Task<ActionResult> DressResult(DressModelViewModel model)
        {
            var scryerDragon = FRHelpers.ParseUrlForDragon(model.ScryerUrl);
            var dressingRoomDragon = FRHelpers.ParseUrlForDragon(model.DressingRoomUrl);

            if (scryerDragon.Age == Data.Age.Hatchling)
            {
                AddErrorNotification($"You can only dress an adult dragon!");
                return RedirectToRoute("ScryerDresser");
            }

            if (scryerDragon.DragonType != dressingRoomDragon.DragonType)
            {
                AddErrorNotification($"The breeds of the two images do not match. Scryer image is a <b>{scryerDragon.DragonType}</b> while the dressing room is a <b>{dressingRoomDragon.DragonType}</b>!");
                return RedirectToRoute("ScryerDresser");
            }

            if (scryerDragon.Gender != dressingRoomDragon.Gender)
            {
                AddErrorNotification($"The genders of the two images do not match. Scryer image is a <b>{scryerDragon.Gender}</b> while the dressing room is a <b>{dressingRoomDragon.Gender}</b>!");
                return RedirectToRoute("ScryerDresser");
            }

            var azureImageService = new AzureImageService();

            var azureImagePreviewPath = $@"previews\dresser\{scryerDragon.ToString().Trim('_')}\{dressingRoomDragon.Apparel?.Replace(',', '-').ToString()}.png";
            if (!azureImageService.Exists(azureImagePreviewPath, out var previewUrl))
            {
                var invisibleDragon = await FRHelpers.GetInvisibleDressingRoomDragon(dressingRoomDragon);
                var baseDragon = await FRHelpers.GetDragonBaseImage(scryerDragon);

                using (var graphics = Graphics.FromImage(baseDragon))
                {
                    graphics.DrawImage(invisibleDragon, new Rectangle(0, 0, 350, 350));
                }

                using (var memStream = new MemoryStream())
                {
                    baseDragon.Save(memStream, ImageFormat.Png);
                    memStream.Position = 0;

                    previewUrl = await azureImageService.WriteImage(azureImagePreviewPath, memStream);
                }
            }

            return View(new DressModelResultViewModel { PreviewUrl = previewUrl });
        }
    }
}