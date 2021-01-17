using Discord;
using Discord.Commands;
using FRTools.Common;
using FRTools.Data;
using FRTools.Discord.Infrastructure;
using HtmlAgilityPack;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace FRTools.Discord.Modules
{
    [Name("Lookup"), Group("lookup"), Alias("lu"), Summary("Lookup related commands")]
    [DiscordHelp("LookupModule")]
    public class LookupModule : BaseModule
    {
        public LookupModule(DataContext dbContext, SettingManager settingManager, FRToolsLogger logger) : base(dbContext, settingManager, logger)
        {
        }

        [Command("dragon"), Name("Dragon"), Alias("d")]
        [DiscordHelp("LookupDragon")]
        public async Task DragonLookup(int id)
        {
            var lookingUpMessage = await ReplyAsync($"Looking up dragon {id}, gimme a moment..");

            var client = new HtmlWeb();
            var dragonProfileDoc = client.Load(string.Format(FRHelpers.DragonProfileUrl, id));

            if (dragonProfileDoc.GetElementbyId("error-404") != null)
            {
                await lookingUpMessage.ModifyAsync(x => x.Content = "This dragon does not exist");
                return;
            }

            var isExalted = dragonProfileDoc.GetElementbyId("exalted-content") != null;

            var embed = new EmbedBuilder()
                .WithUrl(string.Format(FRHelpers.DragonProfileUrl, id))
                .WithThumbnailUrl($"attachment://{id}_350.png")
                .WithFooter(x => x.Text = "Click the image to view a larger version");

            if (isExalted)
            {
                var name = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""content""]/div/div[2]/div/div/div[2]/span").InnerHtml;
                var exaltedToFlight = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""exalted-content""]/div/div[2]/div/p[1]/strong[2]").InnerHtml;

                embed.Title = name;
                embed.Description = $"**{name}** has been exalted to the {FRHelpers.GetFlightFromGodName(exaltedToFlight)} flight.\nThe only info available is lineage.";

                var parentsField = new EmbedFieldBuilder().WithName("Parents").WithIsInline(true);
                var siblingsField = new EmbedFieldBuilder().WithName("Offspring").WithIsInline(true);

                var parentsNodes = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""exalted-content""]/div/div[2]/div/fieldset/div/ul[1]").SelectNodes("li");
                var siblingsNodes = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""exalted-content""]/div/div[2]/div/fieldset/div/ul[2]").SelectNodes("li");

                if (parentsNodes[0].FirstChild.Name == "em")
                    parentsField.Value = "_none_";
                else
                    parentsField.Value = string.Join("\n", parentsNodes.Select(x => $"[{x.FirstChild.InnerHtml}]({x.FirstChild.Attributes["href"].Value})"));

                if (siblingsNodes[0].FirstChild.Name == "em")
                    siblingsField.Value = "_none_";
                else
                    if (siblingsNodes.Count <= 5)
                    siblingsField.Value = string.Join("\n", siblingsNodes.Select(x => $"[{x.FirstChild.InnerHtml}]({x.FirstChild.Attributes["href"].Value})"));
                else
                    siblingsField.Value = string.Join("\n", siblingsNodes.Take(5).Select(x => $"[{x.FirstChild.InnerHtml}]({x.FirstChild.Attributes["href"].Value})")) + $"\n_{siblingsNodes.Count - 5} more.._";

                embed.AddField(parentsField);
                embed.AddField(siblingsField);
            }
            else
            {
                var name = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-header""]/div[1]/span[1]").InnerHtml;
                var ownerNode = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""content""]/div/div[2]/div/div/div[2]/a[1]");
                var level = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-battle""]/div[1]/strong").InnerText.Replace("&infin;", "∞");

                var eggbreakday = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-physical""]/div/div[2]/div[1]/div/div/div[2]/strong").InnerText;
                var dragonAge = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-physical""]/div/div[2]/div[2]/div/div/div[2]").ChildNodes[0].InnerText.Replace("\\n", "").Trim().ToLower();
                var dragonBreed = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-physical""]/div/div[2]/div[2]/div/div/div[2]/strong").InnerText;

                var badges = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-secondary""]/div[2]/div[3]").ChildNodes.Nodes().Where(x => x.Name == "img");
                var gender = badges.First(x => x.ParentNode.Attributes.FirstOrDefault(a => a.Name == "data-tooltip-source")?.Value == "#dragon-profile-icon-sex-tooltip").Attributes["src"].Value.EndsWith("female.png") ? "female" : "male";
                var permaBaby = badges.Any(x => x.Attributes["src"].Value.EndsWith("eternal-youth.png"));

                embed.Title = name;
                embed.Description = $"**{name}** is a level {level}, {(permaBaby ? "**eternal youth**" : dragonAge)} {gender} {dragonBreed} in [{ownerNode.InnerHtml}]({ownerNode.Attributes["href"].Value})'{(ownerNode.InnerHtml.EndsWith("s") ? "" : "s")} lair.";

                var eyeColor = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-physical""]/div/div[2]/div[3]/div/div/div[2]").ChildNodes[0].InnerText.Replace("\\n", "").Trim();
                var eyeType = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-physical""]/div/div[2]/div[3]/div/div/div[2]/strong").InnerText;

                embed.AddField(x => x.WithIsInline(true).WithName("General info").WithValue($"**Eye type:** {eyeColor} {eyeType}\n**Eggday:** {eggbreakday}\n**ID:** {id}"));

                var geneticsNode = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-physical""]/div/div[1]/div[2]");
                var primaryGene = geneticsNode.ChildNodes[1].ChildNodes[1].ChildNodes[3].ChildNodes[3].InnerHtml;
                var primaryColor = geneticsNode.ChildNodes[1].ChildNodes[1].ChildNodes[3].ChildNodes[0].InnerHtml.Replace("\\n", "").Trim();
                var secondaryGene = geneticsNode.ChildNodes[3].ChildNodes[1].ChildNodes[3].ChildNodes[3].InnerHtml;
                var secondaryColor = geneticsNode.ChildNodes[3].ChildNodes[1].ChildNodes[3].ChildNodes[0].InnerHtml.Replace("\\n", "").Trim();
                var tertiaryGene = geneticsNode.ChildNodes[5].ChildNodes[1].ChildNodes[3].ChildNodes[3].InnerHtml;
                var tertiaryColor = geneticsNode.ChildNodes[5].ChildNodes[1].ChildNodes[3].ChildNodes[0].InnerHtml.Replace("\\n", "").Trim();

                embed.AddField(x => x.WithIsInline(true).WithName("\u200B").WithValue("\u200B"));
                embed.AddField(x => x.WithIsInline(true).WithName("Genetics").WithValue($"**Primary:** {primaryColor} {primaryGene}\n**Secondary:** {secondaryColor} {secondaryGene}\n**Tertiary:** {tertiaryColor} {tertiaryGene}\n"));

                var parentsField = new EmbedFieldBuilder().WithName("Parents").WithIsInline(true);
                var siblingsField = new EmbedFieldBuilder().WithName("Offspring").WithIsInline(true);

                if (dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-details-lineage""]/div/div") != null)
                {
                    embed.AddField(x => x.WithIsInline(true).WithName("God").WithValue("This is one of the **eleven elemental deities**; it has no parents or offspring."));
                }
                else
                {
                    var parentsNodes = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-details-lineage""]/div/ul[1]").SelectNodes("li");
                    var siblingsNodes = dragonProfileDoc.DocumentNode.SelectSingleNode(@"//*[@id=""dragon-profile-details-lineage""]/div/ul[2]").SelectNodes("li");

                    if (parentsNodes[0].FirstChild.Name == "em")
                        parentsField.Value = "_none_";
                    else
                        parentsField.Value = string.Join("\n", parentsNodes.Select(x => $"[{x.FirstChild.InnerHtml}]({x.FirstChild.Attributes["href"].Value})"));

                    if (siblingsNodes[0].FirstChild.Name == "em")
                        siblingsField.Value = "_none_";
                    else
                    {
                        if (siblingsNodes.Count <= 5)
                            siblingsField.Value = string.Join("\n", siblingsNodes.Select(x => $"[{x.FirstChild.InnerHtml}]({x.FirstChild.Attributes["href"].Value})"));
                        else
                            siblingsField.Value = string.Join("\n", siblingsNodes.Take(5).Select(x => $"[{x.FirstChild.InnerHtml}]({x.FirstChild.Attributes["href"].Value})")) + $"\n_{siblingsNodes.Count - 5} more.._";
                    }

                    embed.AddField(parentsField);
                    embed.AddField(x => x.WithIsInline(true).WithName("\u200B").WithValue("\u200B"));
                    embed.AddField(siblingsField);
                }
            }

            using (var webClient = new WebClient())
            {
                var dragonImage = webClient.OpenRead(FRHelpers.GetRenderUrl(id));
                await Context.Channel.SendFileAsync(dragonImage, $"{id}_350.png", embed: embed.Build());
            }
            await lookingUpMessage.DeleteAsync();
        }
    }
}