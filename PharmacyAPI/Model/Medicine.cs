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

        public string? ImageUrl { get; set; } = "/images/default.png";

        [NotMapped]
        public IFormFile? ImageFile { get; set; }

        public string SKU { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string PackSize { get; set; } = string.Empty;

        public bool IsFdaApproved { get; set; }
        public bool IsGmpCertified { get; set; }
        public bool IsColdChain { get; set; }

        // --- حقول جديدة ---
        public string? NdcNumber { get; set; }
        public string? BlackBoxWarning { get; set; }
        public string? ClinicalSpecs { get; set; } // مخزنة كنص مفصول بفواصل
                                                   // أضفهم بعد ClinicalSpecs
        public DateTime? FdaApprovalDate { get; set; }
        public string? TemperatureRange { get; set; }
        public decimal? HumidityLimit { get; set; }
        public string? ControlledSubstance { get; set; } = "Non-Controlled";
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}