namespace PharmacyAPI.Models.DTOs
{
    public class MedicineDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int CategoryId { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? NdcNumber { get; set; }
        public string Dosage { get; set; } = string.Empty;
        public string? PackSize { get; set; }
        public bool IsFdaApproved { get; set; }
        public bool IsColdChain { get; set; }
        public string? BlackBoxWarning { get; set; }
        public string? ClinicalSpecs { get; set; }

        // ✅ الصورة بتجي كـ IFormFile مش String
        public IFormFile? ImageFile { get; set; }
    }
}