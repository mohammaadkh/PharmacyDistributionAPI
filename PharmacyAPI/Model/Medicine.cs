namespace PharmacyAPI.Models
{
    public class Medicine
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = "/images/default.png";

        // الربط مع جدول الأصناف
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}