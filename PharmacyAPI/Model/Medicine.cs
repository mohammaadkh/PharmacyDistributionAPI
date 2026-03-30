using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace PharmacyAPI.Models
{
    public class Medicine
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        // هاد الحقل بضل متل ما هو لتخزين مسار الصورة النهائي في قاعدة البيانات
        public string ? ImageUrl { get; set; } = "/images/default.png";

        // --- التعديل الجديد لرفع الصور ---
        [NotMapped] // هاد الوسم ضروري جداً عشان ما يضيف عمود بالداتا بيز للملف نفسه
        public IFormFile? ImageFile { get; set; }

        public string SKU { get; set; } = string.Empty;

        // --- تفاصيل البطاقة في الواجهة ---
        public string Dosage { get; set; } = string.Empty;

        public string Manufacturer { get; set; } = string.Empty;

        public string PackSize { get; set; } = string.Empty;

        // --- الأوسمة والشارات (Badges) ---
        public bool IsFdaApproved { get; set; }

        public bool IsGmpCertified { get; set; }

        public bool IsColdChain { get; set; }

        // --- الربط مع جدول الأصناف ---
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}