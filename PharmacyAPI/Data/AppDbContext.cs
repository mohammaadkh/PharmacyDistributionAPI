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
            // --- 1. ضبط دقة الأرقام المالية (Decimal Precision) ---
            // لضمان دقة الفواصل العشرية وتطابقها مع واجهة رفيقك
            modelBuilder.Entity<Medicine>().Property(m => m.Price).HasPrecision(18, 2);
            modelBuilder.Entity<OrderDetail>().Property(m => m.PriceAtPurchase).HasPrecision(18, 2);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingFees).HasPrecision(18, 2);
                entity.Property(o => o.RegulatoryFees).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            });

            // --- 2. حماية البيانات من الحذف التلقائي (Restrict Delete) ---

            // أ- منع حذف الصنف إذا كان يحتوي على أدوية
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Medicines)
                .WithOne(m => m.Category)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ب- حماية السلة: إذا انحذف المستخدم، لا نريد حذف عناصر السلة أوتوماتيكياً (اختياري للأرشفة) 
            // أو إذا انحذف دواء وهو موجود بسلة حدا، السيستم بيمنع الحذف عشان ما تضيع السلة
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Medicine)
                .WithMany()
                .HasForeignKey(ci => ci.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            // ج- حماية الفواتير (أهم نقطة): منع حذف الدواء إذا كان مرتبطاً بطلب سابق (OrderDetail)
            // هاد بيضمن إنو "سجل المبيعات" بضل سليم حتى لو الدواء انحذف من المتجر
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Medicine)
                .WithMany()
                .HasForeignKey(od => od.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            // د- منع حذف الطلب بالكامل بمجرد حذف المستخدم (لأغراض المحاسبة)
            modelBuilder.Entity<Order>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}