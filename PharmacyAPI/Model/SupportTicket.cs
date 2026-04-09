namespace PharmacyAPI.Models
{
    public class SupportTicket
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Subject { get; set; } = string.Empty;
        public string? AdminReply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        // Open / InProgress / Resolved / Closed
        public string Status { get; set; } = "Open";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}