using FRTools.Data;
using FRTools.Data.DataModels.FlightRisingModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Color = FRTools.Data.Color;

namespace FRTools.Common
{
    public static class FRHelpers
    {
        public const string DressingRoomDummyUrl = "https://www1.flightrising.com/dgen/dressing-room/dummy?breed={0}&gender={1}";
        public const string ScryerUrl = "https://flightrising.com/includes/scryer_getdragon.php?zord={0}";
        public const string DragonProfileUrl = "https://www1.flightrising.com/dragon/{0}";
        public const string DragonProfileUrlNoScenic = "https://www1.flightrising.com/dragon/{0}?view=default";
        public const string DressingRoomDragonApparalUrl = "https://www1.flightrising.com/dgen/dressing-room/dragon?did={0}&apparel={1}";
        public const string DressingRoomDummyApparalUrl = "https://www1.flightrising.com/dgen/dressing-room/dummy?breed={0}&gender={1}&apparel={2}";

        public static string GetRenderUrl(int dragonId) => $"https://www1.flightrising.com/rendern/350/{(Math.Floor(dragonId / 100d) + 1)}/{dragonId}_350.png";

        private static Dictionary<string, DragonCache> Cache { get; } = new Dictionary<string, DragonCache>(StringComparer.InvariantCultureIgnoreCase);

        public static async Task<Bitmap> GetInvisibleDressingRoomDragon(DragonCache dragon)
        {
            var azureImageService = new AzureImageService();
            var apparelIds = dragon.GetApparel();

            if (!apparelIds.Contains(22046))
                apparelIds = apparelIds.Concat(new[] { 22046 }).ToArray();

            var dressingRoomUrl = string.Format(DressingRoomDummyUrl, (int)dragon.DragonType, (int)dragon.Gender) + $"&apparel={string.Join(",", apparelIds)}";

            var azureUrl = $@"dragoncache\{dragon.ToString()}_invisible.png";
            using (var client = new WebClient())
            {
                var invisibleDragonBytesTask = client.DownloadDataTaskAsync(dressingRoomUrl);

                var invisibleDwagonImageBytes = await invisibleDragonBytesTask;
                using (var memStream = new MemoryStream(invisibleDwagonImageBytes, false))
                    await azureImageService.WriteImage(azureUrl, memStream);

                using (var memStream = new MemoryStream(invisibleDwagonImageBytes, false))
                {
                    var invisibleDwagon = (Bitmap)Image.FromStream(memStream);
                    return invisibleDwagon;
                }
            }
        }

        public static async Task<Bitmap> GetInvisibleDressingRoomDragon(string dressingRoomUrl)
        {
            if (dressingRoomUrl.Contains("dgen/dressing-room/dragon"))
            {
                var dragonId = int.Parse(Regex.Match(dressingRoomUrl, @"did=([\d]*)").Groups[1].Value);
                var dragon = GetDragonFromDragonId(dragonId);
                var apparelParse = ParseUrlForDragon(dressingRoomUrl);
                dragon.Apparel = apparelParse.Apparel;
                return await GetInvisibleDressingRoomDragon(dragon);
            }
            return await GetInvisibleDressingRoomDragon(ParseUrlForDragon(dressingRoomUrl));
        }

        public static DragonCache ParseUrlForDragon(string dragonUrl, string skinId = null, int? version = null)
        {
            var dragon = new DragonCache();
            Match regexParse;
            if (dragonUrl.Contains("dgen/dressing-room/dragon"))
            {
                var dragonId = int.Parse(Regex.Match(dragonUrl, @"did=([\d]*)").Groups[1].Value);
                dragon = GetDragonFromDragonId(dragonId);
                dragon.FRDragonId = dragonId;
                if ((regexParse = Regex.Match(dragonUrl, @"apparel=([\d,]*)")).Success)
                    dragon.Apparel = regexParse.Groups[1].Value;
                return dragon;
            }

            if ((regexParse = Regex.Match(dragonUrl, @"gender=([\d]*)")).Success)
                dragon.Gender = (Gender)int.Parse(regexParse.Groups[1].Value);
            if ((regexParse = Regex.Match(dragonUrl, @"breed=([\d]*)")).Success)
                dragon.DragonType = (DragonType)int.Parse(regexParse.Groups[1].Value);
            if ((regexParse = Regex.Match(dragonUrl, @"auth=([a-z0-9]*)")).Success)
                dragon.SHA1Hash = regexParse.Groups[1].Value;
            else
            {
                if (skinId != null && version != null)
                    dragon.SHA1Hash = $"DUMMY_{(int)dragon.DragonType}_{(int)dragon.Gender}_{skinId}_v{version}";
                else
                {
                    dragon.SHA1Hash = $"DUMMY_{(int)dragon.DragonType}_{(int)dragon.Gender}";
                    Debug.WriteLine("Caching dummy image without skin info");
                }
            }

            if (!Cache.TryGetValue(dragon.SHA1Hash, out var cachedDragon))
            {
                using (var ctx = new DataContext())
                {
                    var dbDragon = ctx.DragonCache.FirstOrDefault(x => x.SHA1Hash == dragon.SHA1Hash);
                    if (dbDragon != null)
                        dragon = dbDragon;
                    else
                        ctx.DragonCache.Add(dragon);
                    Cache.Add(dragon.SHA1Hash, dragon);

                    if ((regexParse = Regex.Match(dragonUrl, @"element=([\d]*)")).Success)
                        dragon.Element = (Element)int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"eyetype=([\d]*)")).Success)
                        dragon.EyeType = (EyeType)int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"bodygene=([\d]*)")).Success)
                        dragon.BodyGene = int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"winggene=([\d]*)")).Success)
                        dragon.WingGene = int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"tertgene=([\d]*)")).Success)
                        dragon.TertiaryGene = int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"body=([\d]*)")).Success)
                        dragon.BodyColor = (Color)int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"wings=([\d]*)")).Success)
                        dragon.WingColor = (Color)int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"tert=([\d]*)")).Success)
                        dragon.TertiaryColor = (Color)int.Parse(regexParse.Groups[1].Value);
                    if ((regexParse = Regex.Match(dragonUrl, @"age=([\d]*)")).Success)
                        dragon.Age = (Age)int.Parse(regexParse.Groups[1].Value);

                    if (dragon.Age == Age.Dragon)
                        ctx.SaveChanges();
                }
            }
            else
                dragon = cachedDragon;

            if ((regexParse = Regex.Match(dragonUrl, @"apparel=([\d,]*)")).Success)
                dragon.Apparel = regexParse.Groups[1].Value;

            return dragon;
        }

        private static string ScrapeImageUrl(string scryerHtmlPage)
        {
            var imgTag = Regex.Match(scryerHtmlPage, @"<img[^>]*?src\s*=\s*[""']?([^'"" >]+?)[ '""][^>]*?>");
            return imgTag.Groups[1].Value;
        }

        public static string GetDragonImageUrlFromDragonId(int dragonId)
        {
            using (var client = new WebClient())
            {
                var htmlPage = client.DownloadString(string.Format(ScryerUrl, dragonId));
                return ScrapeImageUrl(htmlPage);
            }
        }

        public static string GetDragonImageUrlFromScryId(int scryId)
        {
            var scryUrl = $"https://www1.flightrising.com/scrying/predict?morph={scryId}";
            using (var client = new WebClient())
            {
                var htmlPage = client.DownloadString(scryUrl);
                var scryPageUrlParse = Regex.Match(htmlPage, @"breed=(\d+)&gender=(\d+)&age=(\d+)&bodygene=(\d+)&body=(\d+)&winggene=(\d+)&wings=(\d+)&tertgene=(\d+)&tert=(\d+)&element=(\d+)&eyetype=(\d+)");
                return GeneratedFRHelpers.GenerateDragonImageUrl(int.Parse(scryPageUrlParse.Groups[1].Value), int.Parse(scryPageUrlParse.Groups[2].Value), int.Parse(scryPageUrlParse.Groups[3].Value),
                    int.Parse(scryPageUrlParse.Groups[4].Value), int.Parse(scryPageUrlParse.Groups[5].Value), int.Parse(scryPageUrlParse.Groups[6].Value),
                    int.Parse(scryPageUrlParse.Groups[7].Value), int.Parse(scryPageUrlParse.Groups[8].Value), int.Parse(scryPageUrlParse.Groups[9].Value),
                    int.Parse(scryPageUrlParse.Groups[10].Value), int.Parse(scryPageUrlParse.Groups[11].Value));
            }
        }

        public static DragonCache GetDragonFromDragonId(int dragonId) => ParseUrlForDragon(GetDragonImageUrlFromDragonId(dragonId));

        public static Task<Bitmap> GetDragonBaseImage(string dragonUrl) => GetDragonBaseImage(ParseUrlForDragon(dragonUrl));

        public static async Task<Bitmap> GetDragonBaseImage(DragonCache dragon)
        {
            Bitmap dwagonImage;
            string azureUrl = $@"dragoncache\{dragon.SHA1Hash}.png";
            var azureImageService = new AzureImageService();

            if (azureImageService.Exists(azureUrl, out _))
            {
                using (var stream = await azureImageService.GetImage(azureUrl))
                    dwagonImage = (Bitmap)Image.FromStream(stream);
                return dwagonImage;
            }

            using (var client = new WebClient())
            {

                var dwagonImageBytes = await client.DownloadDataTaskAsync(dragon.ConstructImageString() ?? string.Format(DressingRoomDummyUrl, (int)dragon.DragonType, (int)dragon.Gender));
                using (var memStream = new MemoryStream(dwagonImageBytes, false))
                    await azureImageService.WriteImage(azureUrl, memStream);

                using (var memStream = new MemoryStream(dwagonImageBytes, false))
                {
                    dwagonImage = (Bitmap)Image.FromStream(memStream);
                    return dwagonImage;
                }
            }
        }

        public static Task<FRUser> GetOrUpdateFRUser(string username, DataContext ctx = null) => GetOrUpdateFRUser(username, null, ctx);
        public static Task<FRUser> GetOrUpdateFRUser(int userId, DataContext ctx = null) => GetOrUpdateFRUser(null, userId, ctx);

        private static Task<FRUser> GetOrUpdateFRUser(string username, int? userId, DataContext ctx = null)
        {
            async Task<FRUser> getFRUser()
            {
                var dispose = false;
                if (dispose = ctx == null)
                    ctx = new DataContext();

                var frUser = ctx.FRUsers.FirstOrDefault(x => x.Username == username || x.FRId == userId);

                if (frUser == null)
                {
                    var (frName, frId) = GetFRUserInfo(username, userId);

                    if (frName == null)
                        return null;

                    frUser = ctx.FRUsers.FirstOrDefault(x => x.Username == frName || x.FRId == frId) ?? ctx.FRUsers.Add(new FRUser());
                    frUser.Username = frName;
                    frUser.FRId = frId;
                    frUser.LastUpdated = DateTime.UtcNow;

                    ctx.SaveChanges();
                    if (dispose)
                        ctx.Dispose();
                }
                else
                {
                    frUser = await frUser.UpdateFRUser();
                    await ctx.SaveChangesAsync();
                }

                if (dispose)
                    ctx.Dispose();

                return frUser;
            }
            return getFRUser();
        }

        public static async Task<FRUser> UpdateFRUser(this FRUser frUser)
        {
            // Only update if it hasn't been a day to avoid spamming FR server
            if (DateTime.UtcNow < frUser.LastUpdated.AddDays(1))
                return frUser;

            var (frName, frId) = GetFRUserInfo(null, frUser.FRId);

            if (frName == null)
                return null;

            frUser.Username = frName;
            frUser.FRId = frId;
            frUser.LastUpdated = DateTime.UtcNow;
            await Task.Delay(50);
            return frUser;
        }

        private static (string Username, int UserId) GetFRUserInfo(string username, int? userId)
        {
            string url = $"https://www1.flightrising.com/clan-profile/{(userId?.ToString() ?? $"n/{username}")}";
            using (var client = new WebClient())
            {
                var userProfilePage = client.DownloadString(url);
                if (userProfilePage.Contains("404 - Page Not Found") || userProfilePage.Contains("404: User not found"))
                    return default;

                var userBio = Regex.Match(userProfilePage, @"<div class=""userdata-section"" style=""height:136px;"">[\s\S]+?<span style=""position:absolute; top:8px; left:8px; color:#731d08; font-weight:bold; font-size:16px;"">\s+([\s\S]+?)\s+</span>[\s\S]+?<span>([\d]+?)</span>[\s\S]+?</div>");
                var frName = userBio.Groups[1].Value;
                var frId = int.Parse(userBio.Groups[2].Value);

                return (frName, frId);
            }
        }

        public static Flight GetFlightFromGodName(string godName)
        {
            switch (godName.ToLower())
            {
                default:
                case "earthshaker":
                    return Flight.Earth;
                case "plaguebringer":
                    return Flight.Plague;
                case "windsinger":
                    return Flight.Wind;
                case "tidelord":
                    return Flight.Water;
                case "stormcatcher":
                    return Flight.Lightning;
                case "icewarden":
                    return Flight.Ice;
                case "shadowbinder":
                    return Flight.Shadow;
                case "lightweaver":
                    return Flight.Light;
                case "arcanist":
                    return Flight.Arcane;
                case "gladekeeper":
                    return Flight.Nature;
                case "flamecaller":
                    return Flight.Fire;
            }
        }
    }
}