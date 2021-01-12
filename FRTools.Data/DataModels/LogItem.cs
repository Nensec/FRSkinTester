using FRTools.Data.DataModels.DiscordModels;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FRTools.Data.DataModels
{
    public class LogItem
    {
        public int Id { get; set; }
        [Required]
        public LogItemSeverity Severity { get; set; }
        [Required]
        public LogItemOrigin Origin { get; set; }
        [Column(TypeName = "datetime2")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }

        public string Message { get; set; }
        public string Exception { get; set; }

        public virtual User User { get; set; }

        public virtual DiscordUser DiscordUser { get; set; }
        public virtual DiscordServer Server { get; set; }
        public virtual DiscordChannel Channel { get; set; }
    }
}