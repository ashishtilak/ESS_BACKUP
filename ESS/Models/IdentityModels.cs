using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ESS.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Company> Companies { get; set; }

        public DbSet<WorkGroups> WorkGroups { get; set; }

        public DbSet<Units> Units { get; set; }

        public DbSet<Designations> Designations { get; set; }

        public DbSet<Grades> Grades { get; set; }

        public DbSet<Departments> Departments { get; set; }

        public DbSet<Stations> Stations { get; set; }

        public DbSet<Sections> Sections { get; set; }

        public DbSet<Contractors> Contractors { get; set; }

        public DbSet<Categories> Categories { get; set; }

        public DbSet<EmpTypes> EmpTypes { get; set; }

        public DbSet<Employees> Employees { get; set; }

        public DbSet<LeaveTypes> LeaveTypes { get; set; }

        public DbSet<LeaveBalance> LeaveBalance { get; set; }

        public DbSet<ReleaseStatus> ReleaseStatus { get; set; }

        public DbSet<ReleaseGroups> ReleaseGroups { get; set; }

        public DbSet<ReleaseStrategies> ReleaseStrategy { get; set; }

        public DbSet<ReleaseStrategyLevels> ReleaseStrategyLevels { get; set; }

        public DbSet<ReleaseAuth> ReleaseAuth { get; set; }

        public DbSet<LeaveApplications> LeaveApplications { get; set; }

        public DbSet<LeaveApplicationDetails> LeaveApplicationDetails { get; set; }

        public DbSet<ApplReleaseStatus> ApplReleaseStatus { get; set; }

        public DbSet<OpenMonth> OpenMonth { get; set; }

        public DbSet<LeaveRules> LeaveRules { get; set; }

        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();
        }
    }
}