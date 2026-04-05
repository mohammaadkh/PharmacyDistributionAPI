using PharmacyAPI.Model;
using System;
using System.Collections.Generic;

namespace PharmacyAPI.Models
{
    public class Order
    {
        public int Id { get; set; }

        // ربط الفاتورة بالمستخدم
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime OrderDate { get; set; } = DateTime.Now;

        // --- الحقول المطلوبة لضمان تطابق الفاتورة وحل أخطاء الـ Context ---

        public decimal Subtotal { get; set; } // المجموع قبل الضرائب والشحن

        public decimal ShippingFees { get; set; } // مصاريف الشحن (145.50$)

        public decimal RegulatoryFees { get; set; } // الرسوم التنظيمية (89.00$)

        public decimal TotalAmount { get; set; } // المجموع النهائي (2,631.50$)

        public string Status { get; set; } = "Pending";
        public string OrderNumber { get; set; } = string.Empty;
        // ربط الفاتورة بتفاصيلها (الأدوية المشتراة)
        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}