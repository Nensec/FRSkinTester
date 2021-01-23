using FRTools.Data.DataModels;
using System;
using System.Collections.Generic;

namespace FRTools.Web.Models
{
    public class LogsViewModel
    {
        public List<LogItem> Logs { get; set; }
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 20;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public LogItemSeverity? Severity { get; set; }
        public LogItemOrigin? Origin { get; set; }
        public int? UserId { get; set; }
        public long? DiscordUser { get; set; }
        public long? DiscordChannel { get; set; }
        public long? DiscordServer { get; set; }
        public string MessageQuery { get; set; }
        public string ExceptionQuery { get; set; }
        public string OrderBy { get; set; } = "id";
        public bool OrderByDescending { get; set; } = true;
    }
}