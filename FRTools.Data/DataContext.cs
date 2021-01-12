using FRTools.Data.DataModels;
using FRTools.Data.DataModels.DiscordModels;
using FRTools.Data.DataModels.FlightRisingModels;
using FRTools.Data.DataModels.NewsReaderModels;
using FRTools.Data.DataModels.PinglistModels;
using FRTools.Data.Migrations;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Data.Entity;

namespace FRTools.Data
{
    // Is EF for this project overkilll? Yes, yes it is.
    // But I cba maintaining db scripts if I ever expand this project later.
    public class DataContext : IdentityDbContext<User, Role, int, UserLogin, UserRole, UserClaim>
    {
        static DataContext()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<DataContext, Configuration>());
        }

        public DataContext() : base("defaultConnection")
        {
        }

        public static DataContext Create()
        {
            return new DataContext();
        }

        // FRTools tables
        public DbSet<Job> Jobs { get; set; }
        public DbSet<ProfileSettings> ProfileSettings { get; set; }
        public DbSet<LogItem> LogItems { get; set; }

        // Skintester tables
        public DbSet<Skin> Skins { get; set; }
        public DbSet<Preview> Previews { get; set; }

        // Newsreader tables
        public DbSet<Topic> Topics { get; set; }
        public DbSet<Post> Posts { get; set; }

        // Pinglist tables
        public DbSet<Pinglist> Pinglists { get; set; }
        public DbSet<PingListEntry> PingListEntries { get; set; }
        public DbSet<PinglistCategory> PinglistCategories { get; set; }

        // Flight rising tables
        public DbSet<DragonCache> DragonCache { get; set; }
        public DbSet<FRUser> FRUsers { get; set; }
        public DbSet<FRDominance> FRDominances { get; set; }

        // Discord tables
        public DbSet<DiscordLog> DiscordLogs { get; set; }
        public DbSet<DiscordServer> DiscordServers { get; set; }
        public DbSet<DiscordChannel> DiscordChannels { get; set; }
        public DbSet<DiscordRole> DiscordRoles { get; set; }
        public DbSet<DiscordSetting> DiscordSettings { get; set; }
        public DbSet<DiscordUser> DiscordUsers { get; set; }
        public DbSet<DiscordServerUser> DiscordServerUsers { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<UserRole>().ToTable("UserRoles");
            modelBuilder.Entity<UserClaim>().ToTable("UserClaims");
            modelBuilder.Entity<UserLogin>().ToTable("UserLogins");

            modelBuilder.Entity<User>().HasOptional(x => x.FRUser).WithOptionalDependent(x => x.User);
            modelBuilder.Entity<User>().HasOptional(x => x.ProfileSettings).WithOptionalPrincipal(x => x.User);
        }
    }
}