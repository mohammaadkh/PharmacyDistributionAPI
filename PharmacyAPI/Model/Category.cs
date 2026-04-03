using PharmacyAPI.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }        
    public List<Medicine> Medicines { get; set; } = new List<Medicine>();
}