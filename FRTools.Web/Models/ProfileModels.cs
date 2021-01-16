using FRTools.Common;
using FRTools.Data.DataModels;
using FRTools.Data.DataModels.PinglistModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace FRTools.Web.Models
{
    public class ViewProfileViewModel
    {
        public User User { get; set; }
        public List<Preview> Previews { get; set; } = new List<Preview>();
        public List<Skin> Skins { get; set; } = new List<Skin>();
        public List<Pinglist> Pinglists { get; set; } = new List<Pinglist>();
        public string CDNBasePath => ConfigurationManager.AppSettings["CDNBasePath"];
        public bool IsOwn { get; set; }

        public Func<string, int, string> GetDummyPreviewImage { get; set; }
    }


    public class EditProfileViewModel : ProfileSettings
    {
        public string Username { get; set; }
    }
}