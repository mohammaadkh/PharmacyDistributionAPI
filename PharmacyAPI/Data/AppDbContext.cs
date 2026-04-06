using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Model;
using PharmacyAPI.Models; // تأكد أن الموديلات كلها بهذا الـ Namespace

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
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Decimal Precision (كما هي لديك)
            modelBuilder.Entity<Medicine>().Property(m => m.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Medicine>().Property(m => m.HumidityLimit).HasPrecision(5, 2);
            modelBuilder.Entity<OrderDetail>().Property(m => m.PriceAtPurchase).HasPrecision(18, 2);
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingFees).HasPrecision(18, 2);
                entity.Property(o => o.RegulatoryFees).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            });

            // 2. Constraints & Indexes
            modelBuilder.Entity<Medicine>(entity =>
            {
                entity.Property(m => m.Dosage).IsRequired().HasMaxLength(50);
                entity.Property(m => m.Manufacturer).IsRequired().HasMaxLength(100);
                entity.Property(m => m.SKU).IsRequired().HasMaxLength(50);
            });

            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            // 3. Relationships (مع ضبط الحذف)
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

            // 4. ✅ SEED DATA - إضافة بيانات تلقائية
            // أولاً: إضافة تصنيفات
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Antibiotics", Description = "Bacterial infections" },
                new Category { Id = 2, Name = "Painkillers", Description = "Pain relief" }
            );

            // ثانياً: إضافة أدوية (تأكد أن الـ CategoryId موجود فوق)
            modelBuilder.Entity<Medicine>().HasData(
                new Medicine
                {
                    Id = 1,
                    Name = "Panadol",
                    CategoryId = 2,
                    Price = 12.50m,
                    StockQuantity = 100,
                    SKU = "PAN-001",
                    Dosage = "500mg",
                    Manufacturer = "GSK",
                    IsFdaApproved = true
                },
                new Medicine
                {
                    Id = 2,
                    Name = "Amoxicillin",
                    CategoryId = 1,
                    Price = 45.00m,
                    StockQuantity = 50,
                    SKU = "AMO-002",
                    Dosage = "250mg",
                    Manufacturer = "Pfizer",
                    IsColdChain = false
                }
            );

            // 5. Notifications Relationship
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}