﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRTools.Data.DataModels.DiscordModels
{
    public class DiscordServer
    {
        public int Id { get; set; }
        public long ServerId { get; set; }
        public string Name { get; set; }
        public ICollection<DiscordChannel> Channels { get; set; } = new HashSet<DiscordChannel>();
        public ICollection<DiscordRole> Roles { get; set; } = new HashSet<DiscordRole>();
    }
}
