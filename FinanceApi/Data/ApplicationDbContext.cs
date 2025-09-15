using Microsoft.EntityFrameworkCore;
using FinanceApi.Models;

namespace FinanceApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Add DbSet properties for each entity

        public DbSet<User> User { get; set; }
        public DbSet<Country> Country { get; set; }
        public DbSet<State> State { get; set; }
        public DbSet<City> City { get; set; }
        public DbSet<Service> Service { get; set; }
        public DbSet<CustomerGroups> CustomerGroups { get; set; }
        public DbSet<SupplierGroups> SupplierGroups { get; set; }
        public DbSet<Region> Region { get; set; }
        public DbSet<Location> Location { get; set; }
        public DbSet<Warehouse> Warehouse { get; set; }
        public DbSet<Deduction> Deduction { get; set; }
        public DbSet<Income> Income { get; set; }
        public DbSet<Customer> Customer { get; set; }
        public DbSet<Supplier> Supplier { get; set; }
        public DbSet<Sales> Sales { get; set; }
        public DbSet<Purchases> Purchases { get; set; }
        public DbSet<SaleLineItems> SaleLineItems { get; set; }
        public DbSet<PurchaseLineItem> PurchaseLineItems { get; set; }
        public DbSet<Meeting> Meetings { get; set; }
        public DbSet<Attendees> Attendees { get; set; }
        public DbSet<PasswordResetToken> PasswordResetToken { get; set; }
        public DbSet<ChartOfAccount> ChartOfAccount { get; set; }
        public DbSet<OpeningBalance> OpeningBalance { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Sale-SaleLineItem relationship
            modelBuilder.Entity<Sales>()
                .HasMany(s => s.LineItems)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Purchase-PurchaseLineItem relationship
            modelBuilder.Entity<Purchases>()
               .HasMany(p => p.LineItems)
               .WithOne()
               .OnDelete(DeleteBehavior.Cascade);


            // Configure User - PasswordResetToken relationship
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OpeningBalance>()
            .Property(e => e.Date)
            .HasColumnType("timestamp without time zone");

        }

    }
}
