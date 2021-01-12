using Microsoft.AspNet.Identity.EntityFramework;

namespace FRTools.Data.DataModels
{
    public class Role : IdentityRole<int, UserRole>
    {
        public static string[] Roles => new[]
        {
            "Log",
            "Admin"
        };
    }
}