using FRTools.Data;
using System.Web.Mvc;

namespace FRTools.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [RoutePrefix("admin")]
    public class AdminController : BaseController
    {
        public AdminController(DataContext dataContext) : base(dataContext)
        {
        }

        [Route(Name = "AdminIndex")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("users", Name = "AdminUsers")]
        public ActionResult Users()
        {
            return View();
        }

        [Route("users/{userId}", Name = "AdminViewUser")]
        public ActionResult ViewUser(int userId)
        {
            return View();
        }

        [Route("users/{userId}/edit", Name = "AdminEditUser")]
        public ActionResult EditUser(int userId)
        {
            return View();
        }

        [Route("users/{userId}/edit", Name = "AdminEditUserPost")]
        [HttpPost]
        public ActionResult EditUser(object model)
        {
            return RedirectToRoute("AdminEditUser");
        }
    }
}