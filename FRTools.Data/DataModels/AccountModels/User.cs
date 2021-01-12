using FRTools.Data.DataModels.FlightRisingModels;
using FRTools.Data.DataModels.PinglistModels;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Collections.Generic;

namespace FRTools.Data.DataModels
{

    public class User : IdentityUser<int, UserLogin, UserRole, UserClaim>
    {
        public virtual ProfileSettings ProfileSettings { get; set; }
        public virtual FRUser FRUser { get; set; }
        public virtual ICollection<Skin> Skins { get; set; } = new HashSet<Skin>();
        public virtual ICollection<Preview> Previews { get; set; } = new HashSet<Preview>();
        public virtual ICollection<Pinglist> Pinglists { get; set; } = new HashSet<Pinglist>();
        public virtual ICollection<LogItem> LogItems { get; set; } = new HashSet<LogItem>();
    }
}