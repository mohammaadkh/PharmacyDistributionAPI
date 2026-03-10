using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Model;
using PharmacyAPI.Models;  
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

                 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
                
            modelBuilder.Entity<Medicine>()
                .Property(m => m.Price)
                .HasPrecision(18, 2);

             
            modelBuilder.Entity<OrderDetail>()
                .Property(m => m.PriceAtPurchase)
                .HasPrecision(18, 2);

            base.OnModelCreating(modelBuilder);
        }
    } 
}