using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserService.Infrastructure.Identity;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence
{
    //The third type parameter (Guid) in IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    //specifies the type of the primary key used by all Identity entities (users, roles, claims, etc.).
    public class UserDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

        public DbSet<Address> Addresses { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<Client> Clients { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(entity =>
            {
                // Configure foreign key explicitly, ensure EF uses the existing UserId column:
                entity.HasOne<ApplicationUser>() // Navigation to ApplicationUser
                    .WithMany(u => u.RefreshTokens) // Collection navigation on ApplicationUser
                    .HasForeignKey(rt => rt.UserId) // Explicit FK property
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Address>(entity =>
            {
                entity.HasOne<ApplicationUser>()      // Navigation to ApplicationUser
                    .WithMany(u => u.Addresses)       // Collection navigation on ApplicationUser
                    .HasForeignKey(a => a.UserId)     // Explicit FK property
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Rename Identity tables here:
            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<ApplicationRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
            builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
            builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
            builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
            builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

            var adminRoleId = Guid.Parse("c4a3298c-6198-4d12-bd1a-56d1d1ce0aa7");
            var customerRoleId = Guid.Parse("38b657f4-ac20-4a5c-b2a3-16dfad61c381");
            var vendorRoleId = Guid.Parse("582880c3-f554-490f-a24e-526db35cffa5");

            builder.Entity<ApplicationRole>().HasData(
               new ApplicationRole
               {
                   Id = adminRoleId,
                   Name = "Admin",
                   NormalizedName = "ADMIN",
                   ConcurrencyStamp = "fdf7ef4f-7fd6-4868-b4d2-35d74e2c833e",
                   Description = "Administrator with full permissions"
               },
               new ApplicationRole
               {
                   Id = customerRoleId,
                   Name = "Customer",
                   NormalizedName = "CUSTOMER",
                   ConcurrencyStamp = "333769be-3a5c-4a2c-a745-17d9ccccc7d7",
                   Description = "Customer with shopping permissions"
               },
               new ApplicationRole
               {
                   Id = vendorRoleId,
                   Name = "Vendor",
                   NormalizedName = "VENDOR",
                   ConcurrencyStamp = "cd00d524-80b9-4715-80d2-c4698b225e09",
                   Description = "Vendor who can manage products"
               }
           );

            builder.Entity<Client>().HasData(
                new Client { ClientId = "web", ClientName = "Web Client", Description = "Web browser clients", IsActive = true },
                new Client { ClientId = "android", ClientName = "Android Client", Description = "Android mobile app", IsActive = true },
                new Client { ClientId = "ios", ClientName = "iOS Client", Description = "iOS mobile app", IsActive = true }
            );
        }
    }
}