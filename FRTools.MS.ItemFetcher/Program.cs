﻿using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels.FlightRisingModels;
using HtmlAgilityPack;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FRTools.MS.ItemFetcher
{
    class Program
    {
        static int _requestsMade = 0;
        static int _noItemFoundCounter = 0;

        static async Task Main()
        {
            using (var ctx = new DataContext())
            {
                var highestItemId = ctx.FRItems.Any() ? ctx.FRItems.Max(x => x.FRId) : 0;

                while (_requestsMade <= 1000 && _noItemFoundCounter < 10)
                {
                    ++highestItemId;
                    Console.WriteLine($"Fetching item: {highestItemId}. Request count: {_requestsMade}.");
                    var item = FetchItem(highestItemId);
                    if (item != null)
                        ctx.FRItems.Add(item);
                    else
                        _noItemFoundCounter++;
                    await Task.Delay(50);
                }
                Console.WriteLine($"Done for now, saving {ctx.ChangeTracker.Entries().Count()} items.");
                await ctx.SaveChangesAsync();
            }
        }

        static FRItem FetchItem(int itemId, string category = "skins")
        {
            _requestsMade++;

            var client = new HtmlWeb();
            var itemDoc = client.Load(string.Format(FRHelpers.ItemFetchUrl, itemId, category));
            var iconUrl = itemDoc.DocumentNode.SelectSingleNode("/div/div[1]/img[2]").GetAttributeValue("src", "/images/cms//.png");

            if (iconUrl == "/images/cms//.png")
            {
                Console.WriteLine($"Item {itemId} does not exist.");
                return null;
            }

            try
            {
                _noItemFoundCounter = 0;
                var categoryMatch = Regex.Match(iconUrl, @"/images/cms/(?<Category>.*)/(\d*).png");
                if (categoryMatch.Groups["Category"].Value != category)
                {
                    Console.WriteLine($"Item {itemId} is not {category}, but is actually {categoryMatch.Groups["Category"]}. Fetching that item instead.");
                    return FetchItem(itemId, categoryMatch.Groups["Category"].Value);
                }

                var item = new FRItem { FRId = itemId, IconUrl = iconUrl, ItemCategory = (FRItemCategory)Enum.Parse(typeof(FRItemCategory), category, true) };
                item.Name = itemDoc.DocumentNode.SelectSingleNode("/div/div[1]/div[1]").InnerText.Trim();
                item.Description = itemDoc.DocumentNode.SelectSingleNode("/div/div[2]").InnerText
                    .Replace('\u000A', '\u0020')
                    .Replace('\u0009', '\u0020')
                    .Replace('\u000D', '\u0020')
                    .Trim();
                item.ItemType = itemDoc.DocumentNode.SelectSingleNode("/div/div[1]/div[2]").InnerText.Trim();
                var rarityUrl = itemDoc.DocumentNode.SelectSingleNode("/div/div[1]/img[1]").GetAttributeValue("src", "");
                var rarityMatch = Regex.Match(rarityUrl, @"../images/layout/tooltips/star-(?<Rarity>\d).png");

                if (rarityMatch.Success)
                    item.Rarity = int.Parse(rarityMatch.Groups["Rarity"].Value);

                switch (item.ItemCategory)
                {
                    case FRItemCategory.Food:
                        item.FoodValue = int.Parse(itemDoc.DocumentNode.SelectSingleNode("/div/div[4]").InnerText);
                        item.FoodType = (FRFoodType)Enum.Parse(typeof(FRFoodType), item.ItemType, true);
                        break;
                    case FRItemCategory.Skins:
                        item.AssetUrl = itemDoc.DocumentNode.SelectSingleNode("/div/div[2]/div/img").GetAttributeValue("src", "");
                        break;
                    case FRItemCategory.Equipment:
                        item.TreasureValue = int.Parse(itemDoc.DocumentNode.SelectSingleNode("/div/div[3]").InnerText);
                        item.AssetUrl = string.Format(FRHelpers.DressingRoomDummyUrl, (int)DragonType.Fae, (int)Gender.Male) + $"&apparel=22046,{item.FRId}";
                        break;
                    case FRItemCategory.Familiar:
                        item.TreasureValue = int.Parse(itemDoc.DocumentNode.SelectSingleNode("/div/div[3]").InnerText);
                        item.AssetUrl = string.Format(FRHelpers.FamiliarArtUrl, item.FRId);
                        break;
                    case FRItemCategory.Battle_Items:
                        item.TreasureValue = int.Parse(itemDoc.DocumentNode.SelectSingleNode("/div/div[3]").InnerText);
                        item.RequiredLevel = int.Parse(itemDoc.DocumentNode.SelectSingleNode("/div/div[4]/strong").InnerText);
                        break;
                    case FRItemCategory.Trinket:
                        item.TreasureValue = int.Parse(itemDoc.DocumentNode.SelectSingleNode("/div/div[3]").InnerText);
                        if (item.ItemType == "Scene")
                            item.AssetUrl = string.Format(FRHelpers.SceneArtUrl, item.FRId);
                        if (item.ItemType == "Forum Vista")
                            item.AssetUrl = string.Format(FRHelpers.VistaArtUrl, item.FRId);
                        break;
                }

                item.AssetUrl = item.AssetUrl?.Replace("https://flightrising.com", "").Replace("https://www1.flightrising.com", "");

                return item;
            }
            catch
            {
                Console.WriteLine($"Item {itemId} threw error, possible deleted?");
                return null;
            }
            finally
            {
                Console.WriteLine($"Finished parsing item {itemId}");
                Console.WriteLine("--------------");
            }
        }
    }
}