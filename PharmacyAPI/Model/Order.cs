using PharmacyAPI.Model;
using PharmacyAPI.Models;

namespace PharmacyAPI.Models // تأكد من وجود حرف الـ s هنا ليتطابق مع باقي الجداول
{
    public class Order
    {
        public int Id { get; set; }

        // ربط الفاتورة بالمستخدم
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending";

        // ربط الفاتورة بتفاصيلها (الأدوية المشتراة)
        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}
