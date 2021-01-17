using FRTools.Common;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace FRTools
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            FRToolsLogger.Setup(Data.DataModels.LogItemOrigin.Web);
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
