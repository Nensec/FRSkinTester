using FRTools.Data;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController(DataContext dataContext) : base(dataContext)
        {
        }

        [Route(Name = "Home")]
        public ActionResult Index() => View();

        [Route("404", Name = "NotFound")]
        public ActionResult NotFound() => View();

        [Route("privacy", Name = "Privacy")]
        public ActionResult Privacy() => View();

        [Route("contact", Name = "Contact")]
        public ActionResult Contact() => View();
    }
}