﻿using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using FRTools.Data;
using FRTools.Discord.Infrastructure;

namespace FRTools.Discord.Modules
{
    [RequireOwner]
    [Name("admin")]
    [NoLog]
    public class AdminModule : BaseModule
    {
        public AdminModule(DataContext dbContext, SettingManager settingManager) : base(dbContext, settingManager)
        {            
        }

        [Name("Set Config"), Command("setconfig")]
        public Task SetConfig(string key, [Remainder]string value)
        {
            if (Context.Channel is IDMChannel)
                return SetGlobalConfig(key, value);

            SettingManager.SetSettingValue(key, value, Context.Guild);
            return ReplyAsync($"Updated Key `{key}` with value `{value}` for guild `{Context.Guild.Name}`");
        }

        [Name("Set Global Config"), Command("setglobalconfig")]
        public Task SetGlobalConfig(string key, [Remainder]string value)
        {
            SettingManager.SetSettingValue(key, value);
            return ReplyAsync($"Updated Key `{key}` with value `{value}` for `Global`");
        }
    }
}
