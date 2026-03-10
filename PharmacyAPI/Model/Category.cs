using PharmacyAPI.Model;

namespace PharmacyAPI.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // الربط: الصنف الواحد يحتوي على قائمة أدوية
        public List<Medicine> Medicines { get; set; } = new List<Medicine>();
    }
}