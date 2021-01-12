namespace FRTools.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Logging : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.DiscordServerUserDiscordRoles", newName: "DiscordRoleDiscordServerUsers");
            DropPrimaryKey("dbo.DiscordRoleDiscordServerUsers");
            CreateTable(
                "dbo.LogItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Severity = c.Int(nullable: false),
                        Origin = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false, precision: 7, storeType: "datetime2", defaultValueSql: "GETUTCDATE()"),
                        Message = c.String(),
                        Exception = c.String(),
                        Channel_Id = c.Int(),
                        DiscordUser_Id = c.Int(),
                        Server_Id = c.Int(),
                        User_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DiscordChannels", t => t.Channel_Id)
                .ForeignKey("dbo.DiscordUsers", t => t.DiscordUser_Id)
                .ForeignKey("dbo.DiscordServers", t => t.Server_Id)
                .ForeignKey("dbo.Users", t => t.User_Id)
                .Index(t => t.Channel_Id)
                .Index(t => t.DiscordUser_Id)
                .Index(t => t.Server_Id)
                .Index(t => t.User_Id);
            
            AddPrimaryKey("dbo.DiscordRoleDiscordServerUsers", new[] { "DiscordRole_Id", "DiscordServerUser_Id" });
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.LogItems", "User_Id", "dbo.Users");
            DropForeignKey("dbo.LogItems", "Server_Id", "dbo.DiscordServers");
            DropForeignKey("dbo.LogItems", "DiscordUser_Id", "dbo.DiscordUsers");
            DropForeignKey("dbo.LogItems", "Channel_Id", "dbo.DiscordChannels");
            DropIndex("dbo.LogItems", new[] { "User_Id" });
            DropIndex("dbo.LogItems", new[] { "Server_Id" });
            DropIndex("dbo.LogItems", new[] { "DiscordUser_Id" });
            DropIndex("dbo.LogItems", new[] { "Channel_Id" });
            DropPrimaryKey("dbo.DiscordRoleDiscordServerUsers");
            DropTable("dbo.LogItems");
            AddPrimaryKey("dbo.DiscordRoleDiscordServerUsers", new[] { "DiscordServerUser_Id", "DiscordRole_Id" });
            RenameTable(name: "dbo.DiscordRoleDiscordServerUsers", newName: "DiscordServerUserDiscordRoles");
        }
    }
}
