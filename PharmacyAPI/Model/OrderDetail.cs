namespace PharmacyAPI.Model
{
    public class OrderDetail
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int MedicineId { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtPurchase { get; set; } // السعر وقت البيع
    }
}
