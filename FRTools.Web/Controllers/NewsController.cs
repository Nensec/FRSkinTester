using FRTools.Data;
using FRTools.Web.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [RoutePrefix("newsreader")]
    public class NewsController : BaseController
    {
        public NewsController(DataContext dataContext) : base(dataContext)
        {
        }

        [Route(Name = "NewsReader")]
        public ActionResult Index() // Todo: Paging for the news topic list? 
        {
            var model = new NewsViewModel();
            model.Topics = DataContext.Topics.OrderByDescending(x => x.FRTopicId).Select(x => new NewsTopicViewModel
            {
                FRTopicId = x.FRTopicId,
                TopicName = x.FRTopicName,
                TotalPosts = x.Posts.Count(),
                DeletedPosts = x.Posts.Count(p => p.Deleted),
                TopicStarterClanId = x.TopicStarterClanId,
                TopicStarer = x.TopicStarter,
                CreatedAt = x.Posts.OrderBy(p => p.TimeStamp).FirstOrDefault().TimeStamp
            }).ToList();

            return View(model);
        }

        [Route("view/{topicId}/{page?}", Name = "ReadNews")]
        public ActionResult Read(int topicId, int? page)
        {
            var model = new NewsTopicViewModel { FRTopicId = topicId };
            var topic = DataContext.Topics.FirstOrDefault(x => x.FRTopicId == topicId);
            if (topic == null)
            {
                AddErrorNotification($"Could not find topic id {topicId}.");
                return RedirectToRoute("NewsReader");
            }
            var posts = topic.Posts.OrderBy(x => x.TimeStamp);
            var firstPost = posts.First();
            model.TopicStarterClanId = topic.TopicStarterClanId;
            model.TopicStarer = topic.TopicStarter;
            model.CreatedAt = firstPost.TimeStamp;
            model.TotalPosts = posts.Count();
            model.DeletedPosts = posts.Count(x => x.Deleted);
            model.TopicName = topic.FRTopicName;
            model.Posts = posts.Skip(model.TotalPosts > 10 ? ((page ?? 1) - 1) * 10 : 0).Take(10).Select(x => new NewsPostViewModel
            {
                FRPostId = x.FRPostId,
                PostAuthor = x.PostAuthor,
                PostAuthorClanId = x.PostAuthorClanId,
                CreatedAt = x.TimeStamp,
                IsDeleted = x.Deleted,
                RawHtmlContent = x.Content,
                Reports = x.Reports,
                ExpectedFRPage = (int)Math.Ceiling(posts.Count(p => !p.Deleted && p.FRPostId <= x.FRPostId) / 10d)
            }).ToList();

            return View(model);
        }

        [Route("viewdeleted/{topicId}/{page?}", Name = "ReadDeletedNews")]
        public ActionResult ReadDeletedOnly(int topicId, int? page)
        {
            var model = new NewsTopicViewModel { FRTopicId = topicId, DeletedOnly = true };
            var topic = DataContext.Topics.FirstOrDefault(x => x.FRTopicId == topicId);
            if (topic == null)
            {
                AddErrorNotification($"Could not find topic id {topicId}.");
                return RedirectToRoute("NewsReader");
            }
            var posts = topic.Posts.OrderBy(x => x.TimeStamp);
            var firstPost = posts.First();
            model.TopicStarterClanId = topic.TopicStarterClanId;
            model.TopicStarer = topic.TopicStarter;
            model.CreatedAt = firstPost.TimeStamp;
            model.TotalPosts = posts.Count();
            model.DeletedPosts = posts.Count(x => x.Deleted);
            model.TopicName = topic.FRTopicName;
            model.Posts = posts.Where(x => x.Deleted).Skip(model.DeletedPosts > 10 ? ((page ?? 1) - 1) * 10 : 0).Take(10).Select(x => new NewsPostViewModel
            {
                FRPostId = x.FRPostId,
                PostAuthor = x.PostAuthor,
                PostAuthorClanId = x.PostAuthorClanId,
                CreatedAt = x.TimeStamp,
                IsDeleted = x.Deleted,
                RawHtmlContent = x.Content,
                Reports = x.Reports
            }).ToList();

            return View("Read", model);
        }

        [Route("report/{postId}", Name = "ReportPost")]
        public ActionResult Report(int postId)
        {
            var post = DataContext.Posts.FirstOrDefault(x => x.FRPostId == postId && x.Deleted);
            if (post != null)
            {
                post.Reports += 1;
                DataContext.SaveChanges();
            }
            return Content("");
        }
    }
};