using PharmacyAPI.Models;

namespace PharmacyAPI.Model
{
    public class OrderDetail
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int MedicineId { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtPurchase { get; set; }

        // --- هدول السطرين "الجوهريين" اللي ناقصينك ---
        public Order Order { get; set; } = null!;      // ربط مع الطلب الأساسي
        public Medicine Medicine { get; set; } = null!; // ربط مع الدواء (هاد اللي رح يشيل الخط الأحمر)
    }
}