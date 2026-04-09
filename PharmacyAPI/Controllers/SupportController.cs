using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using System.Security.Claims;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupportController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SupportController(AppDbContext context)
        {
            _context = context;
        }

        // ───────────────────────────────────────────
        // 1. جلب الأسئلة الشائعة
        // GET /api/support/faqs
        // ───────────────────────────────────────────
        [HttpGet("faqs")]
        public IActionResult GetFaqs()
        {
            var faqs = new[]
            {
                new
                {
                    Id = 1,
                    Category = "Account Setup",
                    Question = "How do I complete the FDA Tier 1 Verification?",
                    Answer = "Upload your valid state pharmacy license or DEA registration in the Organization Profile section. Our compliance team typically reviews documentation within 24-48 business hours. Once verified, a green badge will appear next to your laboratory name."
                },
                new
                {
                    Id = 2,
                    Category = "Account Setup",
                    Question = "Can I manage multiple procurement sites from one account?",
                    Answer = "Yes, you can manage multiple procurement sites from a single PharmaLink account. Navigate to Settings and select 'Manage Locations' to add and configure additional sites."
                },
                new
                {
                    Id = 3,
                    Category = "Order Management",
                    Question = "What are the protocols for cold-chain pharmaceutical shipping?",
                    Answer = "Cold-chain products are shipped in temperature-controlled containers maintaining 2°C - 8°C. All shipments include real-time temperature monitoring and are handled by certified cold-chain logistics partners."
                },
                new
                {
                    Id = 4,
                    Category = "Order Management",
                    Question = "How do I update my lab's billing and tax exemption status?",
                    Answer = "Navigate to Settings > Organization Profile > Billing Information. Upload your tax exemption certificate and our finance team will update your account within 2 business days."
                },
                new
                {
                    Id = 5,
                    Category = "Regulatory Compliance",
                    Question = "What regulatory standards does PharmaLink comply with?",
                    Answer = "PharmaLink complies with FDA 21 CFR Part 11, HIPAA, GDP (Good Distribution Practice), and DEA regulations for controlled substances. All vendors are verified for these standards before listing."
                },
                new
                {
                    Id = 6,
                    Category = "Security & Privacy",
                    Question = "How is my data protected on PharmaLink?",
                    Answer = "All data is encrypted using 256-bit AES encryption. We comply with HIPAA requirements and conduct regular security audits. Your procurement data is never shared with third parties without explicit consent."
                }
            };

            return Ok(faqs);
        }

        // ───────────────────────────────────────────
        // 2. فتح تذكرة دعم
        // POST /api/support/ticket
        // ───────────────────────────────────────────
        [HttpPost("ticket")]
        [Authorize]
        public async Task<IActionResult> SubmitTicket([FromBody] SupportTicketDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Subject))
                return BadRequest(new { message = "موضوع التذكرة مطلوب!" });

            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "رسالة التذكرة مطلوبة!" });

            var userId = GetUserId();

            var ticket = new SupportTicket
            {
                UserId = userId,
                Subject = dto.Subject,
                Message = dto.Message,
                Category = dto.Category ?? "General",
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();

            // إشعار للمستخدم
            await NotificationsController.AddNotification(
                _context,
                userId,
                "تم استلام تذكرتك",
                $"تم استلام تذكرة الدعم رقم #{ticket.Id} بنجاح. سنرد خلال 4 ساعات.",
                "Compliance"
            );

            return Ok(new
            {
                message = "تم إرسال تذكرة الدعم بنجاح!",
                ticketId = ticket.Id,
                status = ticket.Status
            });
        }

        // ───────────────────────────────────────────
        // 3. جلب تذاكر المستخدم مع الرد
        // GET /api/support/tickets
        // ───────────────────────────────────────────
        [HttpGet("tickets")]
        [Authorize]
        public async Task<IActionResult> GetMyTickets()
        {
            var userId = GetUserId();

            var tickets = await _context.SupportTickets
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.Subject,
                    t.Message,
                    t.Category,
                    t.Status,
                    t.CreatedAt,
                    // ✅ أضفنا الرد هون
                    t.AdminReply,
                    t.RepliedAt
                })
                .ToListAsync();

            return Ok(tickets);
        }

        // ───────────────────────────────────────────
        // 4. Admin يشوف كل التذاكر
        // GET /api/support/tickets/all
        // ───────────────────────────────────────────
        [HttpGet("tickets/all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllTickets(string? status = null)
        {
            var query = _context.SupportTickets
                .AsNoTracking()
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            var tickets = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.Subject,
                    t.Category,
                    t.Status,
                    t.Message,
                    t.CreatedAt,
                    // ✅ أضفنا الرد هون
                    t.AdminReply,
                    t.RepliedAt,
                    User = new
                    {
                        t.User.FullName,
                        t.User.Email,
                        t.User.OrganizationType
                    }
                })
                .ToListAsync();

            return Ok(tickets);
        }

        // ───────────────────────────────────────────
        // 5. Admin يرد على التذكرة
        // POST /api/support/tickets/{id}/reply
        // ───────────────────────────────────────────
        [HttpPost("tickets/{id}/reply")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReplyToTicket(
            int id,
            [FromBody] ReplyTicketDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Reply))
                return BadRequest(new { message = "الرد مطلوب!" });

            var ticket = await _context.SupportTickets
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
                return NotFound(new { message = "التذكرة غير موجودة!" });

            if (ticket.Status == "Closed")
                return BadRequest(new { message = "لا يمكن الرد على تذكرة مغلقة!" });

            ticket.AdminReply = dto.Reply;
            ticket.RepliedAt = DateTime.UtcNow;
            ticket.Status = "Resolved";
            await _context.SaveChangesAsync();

            // ✅ إشعار للمستخدم بالرد
            await NotificationsController.AddNotification(
                _context,
                ticket.UserId,
                "رد على تذكرة الدعم",
                $"رد فريق الدعم على تذكرتك رقم #{ticket.Id}: {dto.Reply}",
                "Compliance"
            );

            return Ok(new { message = "تم الرد على التذكرة بنجاح!" });
        }

        // ───────────────────────────────────────────
        // 6. Admin يغير Status التذكرة
        // PATCH /api/support/tickets/{id}/status
        // ───────────────────────────────────────────
        [HttpPatch("tickets/{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTicketStatus(
            int id,
            [FromBody] UpdateTicketStatusDto dto)
        {
            var allowedStatuses = new[] { "Open", "InProgress", "Resolved", "Closed" };
            if (!allowedStatuses.Contains(dto.Status))
                return BadRequest(new { message = "الحالة غير صحيحة!" });

            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new { message = "التذكرة غير موجودة!" });

            ticket.Status = dto.Status;
            await _context.SaveChangesAsync();

            // ✅ إشعار للمستخدم
            await NotificationsController.AddNotification(
                _context,
                ticket.UserId,
                "تحديث تذكرة الدعم",
                $"تذكرتك رقم #{ticket.Id} أصبحت {dto.Status}",
                "Compliance"
            );

            return Ok(new { message = "تم تحديث حالة التذكرة!", newStatus = dto.Status });
        }

        // ───────────────────────────────────────────
        // Helper
        // ───────────────────────────────────────────
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }
    }

    // ───────────────────────────────────────────
    // DTOs
    // ───────────────────────────────────────────
    public class SupportTicketDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Category { get; set; }
    }

    public class UpdateTicketStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class ReplyTicketDto
    {
        public string Reply { get; set; } = string.Empty;
    }
}