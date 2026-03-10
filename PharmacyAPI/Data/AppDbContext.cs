using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Model;
using PharmacyAPI.Models; // أبقينا فقط على Models بالجمع لتجنب التضارب
using System.Collections.Generic;

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

        // يجب أن تكون هذه الدالة داخل قوس الكلاس الرئيسي
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // تحديد دقة السعر في جدول الأدوية
            modelBuilder.Entity<Medicine>()
                .Property(m => m.Price)
                .HasPrecision(18, 2);

            // تحديد دقة السعر في تفاصيل الطلب
            modelBuilder.Entity<OrderDetail>()
                .Property(m => m.PriceAtPurchase)
                .HasPrecision(18, 2);

            base.OnModelCreating(modelBuilder);
        }
    } // هذا القوس يغلق الكلاس
}