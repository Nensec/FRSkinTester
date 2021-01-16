using FRTools.Common;
using FRTools.Data;
using FRTools.Data.DataModels;
using FRTools.Data.DataModels.FlightRisingModels;
using FRTools.Web.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace FRTools.Web.Models
{
    public class BaseSkinModel
    {
        public string CDNBasePath => ConfigurationManager.AppSettings["CDNBasePath"];
    }

    public class UploadModelPost : BaseSkinModel
    {
        [Display(Name = "Name of your skin")]
        public string Title { get; set; }
        [Display(Name = "Description")]
        public string Description { get; set; }
        [Display(Name = "Dragon type")]
        public DragonType DragonType { get; set; }
        [Display(Name = "Gender")]
        public Gender Gender { get; set; }
        [Display(Name = "Skin file")]
        [Required]
        public HttpPostedFileBase Skin { get; set; }
    }

    public class UploadModelPostViewModel : BaseSkinModel
    {
        public string SkinId { get; set; }
        public string SecretKey { get; set; }
        public string PreviewImageUrl { get; set; }
        public string ShareUrl { get; set; }
    }

    public class PreviewModelGet : BaseSkinModel
    {
        public string SkinId { get; set; }
    }

    public class PreviewModelViewModel : BaseSkinModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string SkinId { get; set; }
        public string PreviewUrl { get; set; }
        public double? Coverage { get; set; }
        public User Creator { get; set; }
        public DragonType DragonType { get; set; }
        public Gender Gender { get; set; }
        public SkinVisiblity Visibility { get; set; }
        public bool IsOwn { get; set; }
        public int Version { get; internal set; }
    }

    [SmartRequired]
    public partial class PreviewModelPost : BaseSkinModel
    {
        public PreviewModelPost(string skinId, DragonType dragonType)
        {
            SkinId = skinId;
            DragonType = dragonType;
        }

        public PreviewModelPost() { }

        [IgnoreRequired]
        public string SkinId { get; set; }
        [IgnoreRequired]
        public DragonType DragonType { get; set; }

        [Display(Name = "Dragon ID")]
        public int? DragonId { get; set; }
        public bool SwapSilhouette { get; set; } = false;
        [Display(Name = "Scry image URL")]
        public string ScryerUrl { get; set; }
        [Display(Name = "Dressing image URL")]
        public string DressingRoomUrl { get; set; }

        [IgnoreRequired]
        public bool Force { get; set; }

        public bool IsAncientBreed => DragonType == DragonType.Gaoler || DragonType == DragonType.Banescale || DragonType == DragonType.Veilspun;
    }

    public class PreviewModelPostViewModel : BaseSkinModel
    {
        public PreviewResult Result { get; set; }
        public string SkinId { get; set; }
        public DragonCache Dragon { get; set; }
    }

    public class DeleteSkinPost : BaseSkinModel
    {
        public string SkinId { get; set; }
        public string SecretKey { get; set; }
    }

    public class ManageModelGet : BaseSkinModel
    {
        public string SkinId { get; set; }
        public string SecretKey { get; set; }
    }

    public class ManageModelViewModel : BaseSkinModel
    {
        public Skin Skin { get; set; }
        public string PreviewImageUrl { get; set; }
        public string PreviewUrl { get; set; }
        public string ShareUrl { get; set; }
    }

    public class ManageModelPost : BaseSkinModel
    {
        [Display(Name = "Name of your skin")]
        public string Title { get; set; }
        [Display(Name = "Description")]
        public string Description { get; set; }
        [Display(Name = "Dragon type")]
        public DragonType DragonType { get; set; }
        [Display(Name = "Gender")]
        public Gender Gender { get; set; }
        [Display(Name = "Visibility")]
        public SkinVisiblity Visibility { get; set; }
        public string SkinId { get; set; }
        public string SecretKey { get; set; }
    }

    public class ManageSkinsViewModel : BaseSkinModel
    {
        public List<Skin> Skins { get; set; }
        public Func<string, int, string> GetDummyPreviewImage { get; set; }
    }

    public class BrowseViewModel
    {
        public List<PreviewModelViewModel> Results { get; set; } = new List<PreviewModelViewModel>();
        public int TotalResults { get; set; }
        public BrowseFilterModel Filter { get; set; } = new BrowseFilterModel();
    }

    public class BrowseFilterModel
    {
        public enum SkinType
        {
            Accent,
            Skin
        }

        private readonly List<int> _validPageAmounts = new List<int> { 5, 10, 25, 50 };
        private int _pageAmount = 10;
        private List<DragonType> _dragonTypes = Enum.GetValues(typeof(DragonType)).Cast<DragonType>().ToList();
        private List<Gender> _genders = Enum.GetValues(typeof(Gender)).Cast<Gender>().ToList();
        private List<SkinType> _skinTypes = Enum.GetValues(typeof(SkinType)).Cast<SkinType>().ToList();
        private int _page = 1;

        [Display(Name = "Name")]
        public string Name { get; set; } = "";
        [Display(Name = "Dragon breed")]
        public List<DragonType> DragonTypes { get => _dragonTypes; set => _dragonTypes = value.Distinct().ToList(); }
        [Display(Name = "Gender")]
        public List<Gender> Genders { get => _genders; set => _genders = value.Distinct().ToList(); }
        [Display(Name = "Skin type")]
        public List<SkinType> SkinTypes { get => _skinTypes; set => _skinTypes = value.Distinct().ToList(); }

        public int Page { get => _page; set => _page = value > 1 ? value : 1; }
        [Display(Name = "Page size")]
        public int PageAmount
        {
            get => _pageAmount;
            set => _pageAmount = _validPageAmounts.Aggregate((x, y) => Math.Abs(x - value) < Math.Abs(y - value) ? x : y);
        }
    }

    public class UpdateSkinPost
    {
        [Display(Name = "Skin file")]
        [Required]
        public HttpPostedFileBase Skin { get; set; }

        public string SkinId { get; set; }
        public string SecretKey { get; set; }
    }

    public class ClaimSkinPostViewModel : BaseManageModel
    {
        [Display(Name = "Skin Id")]
        public string SkinId { get; set; }
        [Display(Name = "Secret Key")]
        public string SecretKey { get; set; }
    }
}