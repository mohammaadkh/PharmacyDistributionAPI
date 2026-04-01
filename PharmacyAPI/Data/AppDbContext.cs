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
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- 1. ضبط دقة الأرقام المالية (Decimal Precision) ---
            modelBuilder.Entity<Medicine>().Property(m => m.Price).HasPrecision(18, 2);
            modelBuilder.Entity<OrderDetail>().Property(m => m.PriceAtPurchase).HasPrecision(18, 2);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingFees).HasPrecision(18, 2);
                entity.Property(o => o.RegulatoryFees).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            });

            // --- 2. إعدادات قيود الحقول (Constraints) ---
            modelBuilder.Entity<Medicine>(entity =>
            {
                entity.Property(m => m.Dosage).IsRequired().HasMaxLength(50);
                entity.Property(m => m.Manufacturer).IsRequired().HasMaxLength(100);
                entity.Property(m => m.PackSize).HasMaxLength(50);
                entity.Property(m => m.SKU).IsRequired().HasMaxLength(50);
            });

            // --- 3. إدارة العلاقات وحماية البيانات من الحذف (Relationships & Restrict Delete) ---

            // أ- ربط الصنف بالأدوية (منع حذف صنف يحتوي أدوية)
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Medicines)
                .WithOne(m => m.Category)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ب- ربط المستخدم بالطلبات (حل مشكلة UserId1 وضمان بقاء السجلات المالية)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ج- ربط السلة بالدواء (منع حذف دواء موجود في سلة مستخدم)
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Medicine)
                .WithMany()
                .HasForeignKey(ci => ci.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            // د- ربط تفاصيل الطلب بالدواء (حماية أرشيف المبيعات)
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Medicine)
                .WithMany()
                .HasForeignKey(od => od.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}