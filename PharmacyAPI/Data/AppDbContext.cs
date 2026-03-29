using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Model;
using PharmacyAPI.Models; // تأكد أن المسار صحيح لجميع الموديلات

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
            // 1. ضبط دقة الأرقام العشرية لجدول الأدوية (Price)
            modelBuilder.Entity<Medicine>()
                .Property(m => m.Price)
                .HasPrecision(18, 2);

            // 2. ضبط دقة السعر وقت الشراء في تفاصيل الطلب
            modelBuilder.Entity<OrderDetail>()
                .Property(m => m.PriceAtPurchase)
                .HasPrecision(18, 2);

            // 3. إضافة ضبط الدقة للضرائب ومصاريف الشحن (لضمان تطابق الفاتورة)
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingFees).HasPrecision(18, 2);
                entity.Property(o => o.RegulatoryFees).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            });

            // 4. (إضافي) منع حذف الصنف إذا كان يحتوي على أدوية (أمان البيانات)
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Medicines)
                .WithOne(m => m.Category)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}