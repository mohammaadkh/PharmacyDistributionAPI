using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Model;
using PharmacyAPI.Models;
using System.Security.Claims;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        // ───────────────────────────────────────────
        // 1. جلب كل الإشعارات
        // GET /api/notifications?type=Orders&page=1&pageSize=10
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            string? type = null,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize > 50) pageSize = 50;

            var userId = GetUserId();

            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            // فلترة حسب النوع
            if (!string.IsNullOrEmpty(type))
                query = query.Where(n => n.Type == type);

            var total = await query.CountAsync();
            var unreadCount = await query.CountAsync(n => !n.IsRead);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.IsRead,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                unreadCount,
                page,
                pageSize,
                notifications
            });
        }

        // ───────────────────────────────────────────
        // 2. تعليم إشعار معين كمقروء
        // PATCH /api/notifications/{id}/read
        // ───────────────────────────────────────────
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetUserId();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
                return NotFound(new { message = "الإشعار غير موجود!" });

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تعليم الإشعار كمقروء!" });
        }

        // ───────────────────────────────────────────
        // 3. تعليم كل الإشعارات كمقروءة
        // PATCH /api/notifications/read-all
        // ───────────────────────────────────────────
        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetUserId();

            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (!unreadNotifications.Any())
                return Ok(new { message = "لا يوجد إشعارات غير مقروءة!" });

            unreadNotifications.ForEach(n => n.IsRead = true);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"تم تعليم {unreadNotifications.Count} إشعار كمقروء!" });
        }

        // ───────────────────────────────────────────
        // 4. مسح كل الإشعارات
        // DELETE /api/notifications/clear
        // ───────────────────────────────────────────
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearAllNotifications()
        {
            var userId = GetUserId();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .ToListAsync();

            if (!notifications.Any())
                return Ok(new { message = "لا يوجد إشعارات!" });

            _context.Notifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم مسح كل الإشعارات!" });
        }

        // ───────────────────────────────────────────
        // 5. Helper داخلي لإضافة إشعار
        // بتستخدمه Controllers ثانية مثل OrdersController
        // ───────────────────────────────────────────
        public static async Task AddNotification(
            AppDbContext context,
            int userId,
            string title,
            string message,
            string type)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
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
}