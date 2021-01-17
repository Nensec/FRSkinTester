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
        Debug,
        Info,
        Warning,
        Error
    }

    [Flags]
    public enum LogItemOrigin
    {
        Unknown = -1,
        Discord = 1,
        Web = 2,
        NewsReader = 4,
        DominanceChecker = 8,
        SkinTester = 16
    }
}