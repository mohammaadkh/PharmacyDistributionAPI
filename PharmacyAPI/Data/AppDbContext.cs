 using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Model;
using PharmacyAPI.Models;

namespace PharmacyAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Medicine> Medicines { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        // ✅ جديد
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Decimal Precision
            modelBuilder.Entity<Medicine>()
                .Property(m => m.Price).HasPrecision(18, 2);
            modelBuilder.Entity<OrderDetail>()
                .Property(m => m.PriceAtPurchase).HasPrecision(18, 2);
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingFees).HasPrecision(18, 2);
                entity.Property(o => o.RegulatoryFees).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            });

            // Constraints
            modelBuilder.Entity<Medicine>(entity =>
            {
                entity.Property(m => m.Dosage).IsRequired().HasMaxLength(50);
                entity.Property(m => m.Manufacturer).IsRequired().HasMaxLength(100);
                entity.Property(m => m.PackSize).HasMaxLength(50);
                entity.Property(m => m.SKU).IsRequired().HasMaxLength(50);
            });

            // Unique Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Relationships
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Medicines)
                .WithOne(m => m.Category)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Medicine)
                .WithMany()
                .HasForeignKey(ci => ci.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Medicine)
                .WithMany()
                .HasForeignKey(od => od.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SupportTicket>()
                 .HasOne(t => t.User)
                 .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // أضفه مع باقي الـ Decimal Precision
            modelBuilder.Entity<Medicine>()
                .Property(m => m.HumidityLimit)
                .HasPrecision(5, 2);

            // ✅ جديد — Notifications
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications) // ✅ حدد الـ navigation property
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}