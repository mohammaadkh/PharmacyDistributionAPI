namespace PharmacyAPI.Models.DTOs
{
    public class ProductDetailsDto
    {
        public int Id { get; set; }
        public required string Name { get; set; } // أضفنا required
        public required string Description { get; set; } // أضفنا required
        public required string NdcNumber { get; set; } // أضفنا required
        public string? ManufacturerName { get; set; } // أضفنا ? لجعلها اختيارية
        public string? CategoryName { get; set; }
        public string? StorageCondition { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsFdaApproved { get; set; }
        public bool IsColdChain { get; set; }
        public string? BlackBoxWarning { get; set; }
        public List<string> ClinicalSpecs { get; set; } = new(); // قيمة افتراضية قائمة فارغة
    }
}