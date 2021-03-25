﻿using Discord;
using Discord.Commands;
using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Data.DataModels.FlightRisingModels;
using FRTools.Discord.Infrastructure;
using FRTools.Discord.Preconditions;
using FRTools.Tools.SkinTester;
using System;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FRTools.Discord.Modules
{
    [Name("Skin Tester"), Group("skintester"), Alias("st"), Summary("Skin Tester related commands")]
    [DiscordHelp("SkinTesterModule")]
    public class SkinTesterModule : BaseModule
    {
        public SkinTesterModule(DataContext dbContext, SettingManager settingManager) : base(dbContext, settingManager)
        {
        }

        [Command("lookup"), Name("Lookup"), Alias("lu")]
        [DiscordHelp("SkinTesterLookup")]
        public async Task Lookup(string skinId)
        {
            var skin = DbContext.Skins.Include(x => x.Creator.FRUser).FirstOrDefault(x => x.GeneratedId == skinId);
            if (skin == null)
            {
                if (skinId.All(char.IsDigit))
                {
                    try
                    {
                        var intSkinId = int.Parse(skinId);
                        var realItemSearch = DbContext.FRItems.FirstOrDefault(x => x.FRId == intSkinId && x.ItemCategory == FRItemCategory.Skins);
                        if (realItemSearch != null)
                        {
                            await ReplyAsync(embed: ErrorEmbed($"Skin not found, however there is a Flight Rising skin with that ID: `{realItemSearch.Name}`.\nIf you meant to search for a real skin please use the following command instead: `{SettingManager.GetSettingValue("GUILDCONFIG_PREFIX", Context.Guild)}lookup skin {realItemSearch.FRId}`").Build());
                            return;
                        }
                    }
                    catch { }
                }

                await ReplyAsync(embed: ErrorEmbed("Skin not found.").Build());
            }
            else
            {
                var previewUrl = (await SkinTester.GenerateOrFetchDummyPreview(skin.GeneratedId, skin.Version)).Urls[0];

                var embed = new EmbedBuilder();
                embed.WithTitle(skin.Title ?? "_No title_");
                embed.WithImageUrl($"{CDNBasePath}{previewUrl}");
                embed.WithDescription(skin.Description ?? "_No description_");
                embed.WithAuthor(new EmbedAuthorBuilder()
                    .WithName(skin.Creator?.UserName ?? "Anonymous")
                    .WithUrl(skin.Creator != null ? $"{WebsiteBaseUrl}/profile/{skin.Creator.UserName}" : null));
                embed.WithFooter(new EmbedFooterBuilder().WithText($"Version {skin.Version}"));
                embed.WithUrl($"{WebsiteBaseUrl}/skintester/preview/{skinId}");

                await ReplyAsync(embed: embed.Build());
            }
        }

        [Group("preview"), Name("Preview"), Alias("p")]
        [DiscordHelp("SkinTesterPreview")]
        public class SkinTesterPreview : BaseModule
        {
            public SkinTesterPreview(DataContext dbContext, SettingManager settingManager) : base(dbContext, settingManager)
            {
            }

            [Command]
            public async Task Preview(string skinId, int dragonId, [ExactValuePrecondition("-apparel")] string apparel = null)
            {
                await StartPreview().ContinueWith(async msg =>
                {
                    var result = await SkinTester.GenerateOrFetchPreview(skinId, dragonId);
                    if (!result.Success)
                        await msg.Result.ModifyAsync(x => x.Embed = ErrorEmbed(result.GetDiscordErrorMessage).Build());
                    else
                    {
                        SaveStatistics(result);
                        await msg.Result.ModifyAsync(x => x.Embed = GeneratedPreviewEmbed(result, apparel != null));
                    }
                });
            }

            [Command]
            public async Task Preview(string skinId, int dragonId, [ExactValuePrecondition("-s")] string swapSilhouette, [ExactValuePrecondition("-apparel")] string apparel = null)
            {
                await StartPreview().ContinueWith(async msg =>
                {
                    var result = await SkinTester.GenerateOrFetchPreview(skinId, dragonId, true);
                    if (!result.Success)
                        await msg.Result.ModifyAsync(x => x.Embed = ErrorEmbed(result.GetDiscordErrorMessage).Build());
                    else
                    {
                        SaveStatistics(result);
                        await msg.Result.ModifyAsync(x => x.Embed = GeneratedPreviewEmbed(result, apparel != null));
                    }
                });
            }

            [Command]
            public async Task Preview(string skinId, string dragonUrl, [ExactValuePrecondition("-apparel")] string apparel = null)
            {
                await StartPreview().ContinueWith(async msg =>
                {
                    var result = await SkinTester.GenerateOrFetchPreview(skinId, dragonUrl);
                    if (!result.Success)
                        await msg.Result.ModifyAsync(x => x.Embed = ErrorEmbed(result.GetDiscordErrorMessage).Build());
                    else
                    {
                        SaveStatistics(result);
                        await msg.Result.ModifyAsync(x => x.Embed = GeneratedPreviewEmbed(result, apparel != null));
                    }
                });
            }

            private async Task<IUserMessage> StartPreview() => await ReplyAsync(embed: new EmbedBuilder().WithTitle("Generating your preview..").WithDescription("I'm fetching all the required data to preview your skin, this takes a moment..").Build());

            private void SaveStatistics(PreviewResult previewResult)
            {
                using (var ctx = new DataContext())
                {
                    foreach (var url in previewResult.Urls.Where(url => !ctx.Previews.Where(x => x.Skin.Id == previewResult.Skin.Id).Any(x => x.PreviewImage == url)))
                    {
                        ctx.Previews.Add(new Preview
                        {
                            Skin = ctx.Skins.Find(previewResult.Skin.Id),
                            DragonId = previewResult.Dragon.FRDragonId,
                            ScryerUrl = previewResult.DragonUrl,
                            PreviewImage = url,
                            DragonData = previewResult.Dragon.ToString(),
                            PreviewTime = DateTime.UtcNow,
                            Requestor = null,
                            Version = previewResult.Skin.Version
                        });
                    }
                }
            }

            private Embed GeneratedPreviewEmbed(PreviewResult previewResult, bool showApparel = false)
            {
                var botAssembly = Assembly.GetAssembly(typeof(BaseModule));
                var modules = botAssembly.DefinedTypes.Where(x =>
                {
                    try
                    {
                        return !x.IsNested && x.BaseType == typeof(BaseModule);
                    }
                    catch
                    {
                        return false;
                    }
                });

                var embed = new EmbedBuilder();
                embed.WithDescription($"Here is your preview on **{previewResult.Skin.Title ?? previewResult.Skin.GeneratedId}**");
                if (!showApparel && previewResult.Urls.Length > 1)
                    embed.Description += "\r\n\nYou can show the apparel version of the preview by adding **-apparel** at the end of the preview command.";

                embed.WithFields(new EmbedFieldBuilder().WithName("Dragon preview").WithValue($"[click here]({CDNBasePath}{previewResult.Urls[0]})"));
                if (previewResult.Urls.Length > 1)
                    embed.WithFields(new EmbedFieldBuilder().WithName("Apparel preview").WithValue($"[click here]({CDNBasePath}{previewResult.Urls[1]})"));
                if (!showApparel || previewResult.Urls.Length == 1)
                    embed.WithImageUrl($"{CDNBasePath}{previewResult.Urls[0]}");
                else
                    embed.WithImageUrl($"{CDNBasePath}{previewResult.Urls[1]}");

                embed.WithAuthor(new EmbedAuthorBuilder().WithName($"{previewResult.Skin.Title ?? previewResult.Skin.GeneratedId} v{previewResult.Skin.Version}"));
                embed.WithThumbnailUrl($"{CDNBasePath}/previews/{previewResult.Skin.GeneratedId}/{(previewResult.Skin.Version == 1 ? "" : $@"{previewResult.Skin.Version}/")}preview.png");
                return embed.Build();
            }
        }

        [Command("manage")]
        public override Task ManageModule() => base.ManageModule();
    }
}
