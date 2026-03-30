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
            // لضمان حساب الكسور بدقة في الأسعار والفواتير وتجنب أخطاء التقريب
            modelBuilder.Entity<Medicine>().Property(m => m.Price).HasPrecision(18, 2);
            modelBuilder.Entity<OrderDetail>().Property(m => m.PriceAtPurchase).HasPrecision(18, 2);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingFees).HasPrecision(18, 2);
                entity.Property(o => o.RegulatoryFees).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            });

            // --- 2. إعدادات الحقول الجديدة لمطابقة الواجهة (Constraints) ---
            modelBuilder.Entity<Medicine>(entity =>
            {
                // إجبار النظام على وجود هذه القيم لمنع حدوث مشاكل في عرض واجهة المستخدم
                entity.Property(m => m.Dosage).IsRequired().HasMaxLength(50);
                entity.Property(m => m.Manufacturer).IsRequired().HasMaxLength(100);
                entity.Property(m => m.PackSize).HasMaxLength(50);
                entity.Property(m => m.SKU).IsRequired().HasMaxLength(50);
            });

            // --- 3. حماية البيانات من الحذف التلقائي (Restrict Delete) ---
            // ملاحظة: هذه القواعد تضمن بقاء سجلات الصيدلية سليمة حتى عند محاولة حذف عناصر مرتبطة

            // أ- منع حذف الصنف (Category) إذا كان يضم أدوية
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Medicines)
                .WithOne(m => m.Category)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ب- حماية السلة: منع حذف الدواء إذا كان مضافاً لسلة أي مستخدم حالياً
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Medicine)
                .WithMany()
                .HasForeignKey(ci => ci.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            // ج- حماية الأرشيف: منع حذف الدواء إذا كان مسجلاً في فاتورة قديمة (OrderDetail)
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Medicine)
                .WithMany()
                .HasForeignKey(od => od.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);

            // د- حماية المحاسبة: منع حذف الطلبات (Orders) المرتبطة بمستخدم، لضمان صحة التقارير المالية
            modelBuilder.Entity<Order>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}