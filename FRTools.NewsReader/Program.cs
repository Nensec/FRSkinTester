using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Data.DataModels.NewsReaderModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FRTools.NewsReader
{
    class Program
    {
        private const int _initialDelay = 30000;

        static int _delay = _initialDelay;
        static int Delay
        {
            get => _delay;
            set => _delay = value > _initialDelay ? _initialDelay : value;
        }

        static async Task Main()
        {
            FRToolsLogger.Setup();

            var loop = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        var changeDetected = await ParseNewsForum();
                        if (changeDetected)
                            Delay /= 2;
                        else if (Delay < _initialDelay)
                            Delay *= 2;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception! {ex.Message}");
                    }
                    Console.WriteLine($"Waiting {Delay} milliseconds before checking again..");
                    await Task.Delay(Delay);
                }
            });

            await Task.Delay(-1);

            Console.ReadKey();
        }

        private static async Task<bool> ParseNewsForum()
        {
            bool changed = false;
            using (var client = new WebClient())
            {
                Console.WriteLine("Downloading news forum..");
                var mainNewsForum = client.DownloadString("https://www1.flightrising.com/forums/ann");

                Console.WriteLine("Parsing topic rows..");
                var topicRows = Regex.Matches(mainNewsForum, @"<tr+\s+[a-zA-Z]+=+[""a-z]+>[\s\S]+?</tr>").Cast<Match>();

                var newsTopics = topicRows.Select(x => x.Groups[0].Value).Where(x => !x.Contains("poststatus-locked")).ToList();
                Console.WriteLine($"Found {newsTopics.Count} that are not locked, parsing those..");
                foreach (var newsTopic in newsTopics)
                {
                    var newsTopicInfo = Regex.Match(newsTopic, @"<td class=""topic"">[.\s\S]+<a href=""(https://www1.flightrising.com/forums/ann/(\d+))"" class=""forumlink"">([\s\S]+?)</a>[\s\S]+?<a href=""https://www1.flightrising.com/clan-profile/(\d+)"">([\[\]a-zA-Z ]+?)</a>");
                    var topicUrl = newsTopicInfo.Groups[1].Value;
                    var topicId = Convert.ToInt32(newsTopicInfo.Groups[2].Value);
                    var topicName = newsTopicInfo.Groups[3].Value;
                    var authorClanId = Convert.ToInt32(newsTopicInfo.Groups[4].Value);
                    var authorName = newsTopicInfo.Groups[5].Value;

                    var claimedReplies = Convert.ToInt32(Regex.Match(newsTopic, @"<div class=""activity-replies"">\s+(\d+)\s+</div>").Groups[1].Value);
                    var totalPages = Convert.ToInt32(Regex.Matches(newsTopic, $@"<a href=""https://www1.flightrising.com/forums/ann/{topicId}/(\d+)"">").Cast<Match>().Last().Groups[1].Value);

                    var lastPostInfo = Regex.Match(newsTopic, @"<td class='lastpost'>[.\s\S]+<a href=""https://www1.flightrising.com/clan-profile/(\d+)"">([a-zA-Z0-9]+)</a>[\s\S]+<br>\s+?([a-zA-Z]+ \d+, \d{4} \d\d:\d\d:\d\d)");
                    var lastPostAuthorClanId = Convert.ToInt32(lastPostInfo.Groups[1].Value);
                    var lastPostAuthor = lastPostInfo.Groups[2].Value;
                    var lastPostTimestamp = DateTime.ParseExact(lastPostInfo.Groups[3].Value, "MMM dd, yyyy HH:mm:ss", CultureInfo.InvariantCulture);

                    Console.WriteLine($"Found post: \"{topicName}\" ({totalPages} pages) id: {topicId}, claimed replies: {claimedReplies}, last post: {lastPostAuthor} id: {lastPostAuthorClanId}");

                    using (var ctx = new DataContext())
                    {
                        bool parseTopic = false;
                        Console.WriteLine("Checking if topic is known..");
                        var topic = ctx.Topics.FirstOrDefault(x => x.FRTopicId == topicId);
                        if (topic == null)
                        {
                            Console.WriteLine("New topic! Adding topic to database");
                            ctx.Topics.Add(topic = new Topic
                            {
                                FRTopicId = topicId,
                                TopicStarter = authorName,
                                TopicStarterClanId = authorClanId,
                                FRClaimedReplyCount = claimedReplies,
                                FRTopicName = topicName
                            });
                            ctx.SaveChanges();
                            parseTopic = true;
                        }
                        else
                        {
                            Console.WriteLine("Topic known, checking for changes..");
                            topic.FRClaimedReplyCount = claimedReplies;

                            if (topic.Posts.OrderBy(x => x.TimeStamp).LastOrDefault()?.TimeStamp != lastPostTimestamp)
                            {
                                Console.WriteLine("New post was added since last check");
                                parseTopic = true;
                            }
                            else if (topic.FRClaimedReplyCount != claimedReplies)
                            {
                                Console.WriteLine("Reply count does not match up with known data, possibly deleted post!");
                                parseTopic = true;
                            }
                            else if (topic.FRTopicName != topicName)
                            {
                                Console.WriteLine("Topic name changed, saving changes and moving on to next post");
                                topic.FRTopicName = topicName;
                                ctx.SaveChanges();
                            }
                            else
                                Console.WriteLine("Nothing changed, skipping news post");
                        }
                        if (parseTopic)
                            changed = await ParseNewsTopic(topicId, totalPages, claimedReplies, ctx, topic, lastPostAuthorClanId, lastPostTimestamp) || changed;
                    }
                }
            }
            return changed;
        }

        private static async Task<bool> ParseNewsTopic(int topicId, int expectedPages, int claimedReplies, DataContext ctx, Topic topic, int lastPostAuthorClanId, DateTime lastPostTimestamp)
        {
            bool changeFound = false;
            bool skipPaginationCheck = false;
            var onLastPage = true;

            bool CheckLastPostDeletion(List<Match> posts)
            {
                onLastPage = false;
                if (!posts.Any(x =>
                    lastPostTimestamp == DateTime.ParseExact(x.Groups[4].Value.Replace("</strong>", ""), "MMMM dd, yyyy HH:mm:ss", CultureInfo.InvariantCulture) &&
                    lastPostAuthorClanId == Convert.ToInt32(x.Groups[2].Value)))
                {
                    Console.WriteLine("Latest post seems to have been deleted before saving it, skipping checking remaining topic..");
                    return true;
                }
                return false;
            }

            using (var client = new WebClient())
            {
                for (int i = expectedPages; i > 0; i--)
                {
                    Console.WriteLine($"Navigating to page {i}..");
                    var forumPage = client.DownloadString($"https://www1.flightrising.com/forums/ann/{topicId}/{i}");
                    var postsPattern = @"<div id=""post_(\d+)"".+>[.\s\S]+?<div class=""post-author-username-frame"">[.\s\S]+?<a href=""https://www1.flightrising.com/clan-profile/(\d+)"" class=""post-author-username"">([a-zA-Z0-9]+)</a>[.\s\S]+?<div class=""post-timestamp""><strong>([a-zA-Z]+ \d+, \d{4}</strong> \d\d:\d\d:\d\d)[\s\S]+?<div class=""post-text-content"">([.\s\S]+?)(?=<div class=""post-text-signature"">|(?=<div class=""post-text-footer"">|(?=<div class=""common-pagination"">|(?=<div id=""post_\d+"".+>|<div id=""topic-footer"">))))";
                    var posts = Regex.Matches(forumPage, postsPattern).Cast<Match>().ToList();
                    Console.WriteLine($"Found {posts.Count} posts on this page");

                    if (!skipPaginationCheck)
                    {
                        skipPaginationCheck = true;
                        var pagination = Regex.Match(forumPage, @"<div class=""common-pagination-numbers"">[\s\S]+?</div>");
                        if (pagination.Success)
                        {
                            var actualPages = Regex.Matches(pagination.Value, @"<a href=""https://www1.flightrising.com/forums/ann/\d+/(\d+)"">\d+</a>|<strong>(\d+)</strong>");
                            var realexpectedPages = actualPages.Cast<Match>().Max(x => Convert.ToInt32(x.Groups[1].Success ? x.Groups[1].Value : x.Groups[2].Value));
                            if (realexpectedPages > expectedPages)
                            {
                                Console.WriteLine($"Forum lied to us, real page count is: {realexpectedPages}. Parsing additional pages before proceeding..");
                                for (int iex = realexpectedPages; iex > expectedPages; iex--)
                                {
                                    Console.WriteLine($"Navigating to page {iex}..");
                                    var postsEx = Regex.Matches(client.DownloadString($"https://www1.flightrising.com/forums/ann/{topicId}/{iex}"), postsPattern).Cast<Match>().ToList();
                                    Console.WriteLine($"Found {postsEx.Count} posts on this page");
                                    if(onLastPage && CheckLastPostDeletion(postsEx))                                    
                                        break;
                                    
                                    if (await ParseNewsTopicEx(postsEx, ctx, topic))
                                        changeFound = true;
                                }
                                Console.WriteLine($"continuing parsing page {i}..");
                            }
                        }
                    }
                    if (onLastPage && CheckLastPostDeletion(posts))
                        break;

                    if (await ParseNewsTopicEx(posts, ctx, topic))
                        changeFound = true;
                    else if (changeFound && claimedReplies > 10 && posts.Count == 10)
                    {
                        Console.WriteLine("Posts seem to be as expected, skipping checking remaining topic..");
                        break;
                    }
                }
            }
            return changeFound;
        }

        private static async Task<bool> ParseNewsTopicEx(List<Match> posts, DataContext ctx, Topic topic)
        {
            bool changeFound = false;

            foreach (var forumPost in posts)
            {
                var postId = Convert.ToInt32(forumPost.Groups[1].Value);
                Console.Write($"Checking if post {postId} is known..");

                var post = ctx.Posts.FirstOrDefault(x => x.FRPostId == postId);
                if (post == null)
                {
                    Console.WriteLine($" New Post! Adding post to database");
                    var postAuthorClanId = Convert.ToInt32(forumPost.Groups[2].Value);
                    var postAuthorName = forumPost.Groups[3].Value;
                    var postTimeStamp = DateTime.ParseExact(forumPost.Groups[4].Value.Replace("</strong>", ""), "MMMM dd, yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    var postInfoContent = forumPost.Groups[5].Value.Trim();
                    var openingDivs = Regex.Matches(postInfoContent, "<div");
                    var closingDivs = Regex.Matches(postInfoContent, "</div>");

                    if (closingDivs.Count > openingDivs.Count)
                        postInfoContent = postInfoContent.Remove(closingDivs[openingDivs.Count].Index).Trim();

                    topic.Posts.Add(new Post
                    {
                        FRPostId = postId,
                        PostAuthor = postAuthorName,
                        PostAuthorClanId = postAuthorClanId,
                        Content = postInfoContent,
                        TimeStamp = postTimeStamp
                    });
                    changeFound = true;
                    ctx.SaveChanges();
                }
                else
                    Console.WriteLine(" Nope.");
            }
            if (!changeFound)
            {
                var firstPostId = Convert.ToInt32(posts.First().Groups[1].Value);
                var lastPostId = Convert.ToInt32(posts.Last().Groups[1].Value);
                Console.WriteLine($"Checking if any post is deleted between first post (id: {firstPostId}) and last post (id: {lastPostId})");

                var expectedPosts = topic.Posts.Where(x => !x.Deleted && x.FRPostId >= firstPostId && x.FRPostId <= lastPostId).Select(x => new Post { Id = x.Id, FRPostId = x.FRPostId }).ToList();
                var givenPostIds = posts.Select(x => Convert.ToInt32(x.Groups[1].Value)).ToList();

                if (!expectedPosts.Select(x => x.FRPostId).SequenceEqual(givenPostIds))
                {
                    Console.WriteLine("Delete detected! Expected posts do not match up. Checking which post is deleted..");
                    foreach (var expectedPost in expectedPosts)
                        if (!givenPostIds.Contains(expectedPost.FRPostId))
                        {
                            Console.WriteLine($"Found {expectedPost.FRPostId} to be deleted!");
                            ctx.Posts.Find(expectedPost.Id).Deleted = true;
                        }
                    changeFound = true;
                    ctx.SaveChanges();
                }
            }
            await Task.Delay(100);
            return changeFound;
        }
    }

}
