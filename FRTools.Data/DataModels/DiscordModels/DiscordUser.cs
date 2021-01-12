using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRTools.Data.DataModels.DiscordModels
{
    public class DiscordUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public int Discriminator { get; set; }
        public long UserId { get; set; }
        public virtual ICollection<DiscordServerUser> Servers { get; set; } = new HashSet<DiscordServerUser>();
        public virtual ICollection<LogItem> LogItems { get; set; } = new HashSet<LogItem>();

        public override string ToString()
        {
            return $"{Username}#{Discriminator}";
        }
    }
}
