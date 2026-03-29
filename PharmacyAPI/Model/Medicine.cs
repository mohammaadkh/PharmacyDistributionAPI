namespace PharmacyAPI.Models
{
    public class Medicine
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        // تم تغيير الاسم لـ StockQuantity لتعبر عن المخزون بالمستودع
        public int StockQuantity { get; set; }

        public string ImageUrl { get; set; } = "/images/default.png";

        // --- التعديلات الجديدة المطابقة للواجهة ---

        // كود المنتج الفريد الظاهر تحت اسم الدواء في السلة
        public string SKU { get; set; } = string.Empty;

        // لإظهار العلامة الخضراء (FDA Approved)
        public bool IsFdaApproved { get; set; }

        // لإظهار العلامة الزرقاء (Cold Chain Required) للأدوية التي تحتاج تبريد
        public bool IsColdChain { get; set; }

        // --- الربط مع جدول الأصناف ---
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}