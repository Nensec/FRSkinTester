using System;
using System.ComponentModel;

namespace FRTools.Data.DataModels
{
    public enum SkinVisiblity
    {
        [Description("Skin is visible everywhere")]
        Visible,
        [Description("Hide skin from browse only")]
        HideFromBrowse,
        [Description("Hide skin everywhere")]
        HideEverywhere
    }

    public enum JobStatus
    {
        Running,
        Finished,
        Error,
        Cancelled,
        FinishedWithErrors,
        UserConfirmedDone
    }

    public enum LogItemSeverity
    {
        Info,
        Warning,
        Error
    }

    [Flags]
    public enum LogItemOrigin
    {
        Discord = 1,
        Web = 2,
        Skintester = 4,
        Newsreader = 10,
        Pinglist = 16,
        Profile = 32,
        AccountManage = 64,
        Scryer = 130,
        Dominance = 256
    }
}