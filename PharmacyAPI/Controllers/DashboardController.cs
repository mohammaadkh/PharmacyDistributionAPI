using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using System.Security.Claims;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // ───────────────────────────────────────────
        // 1. داشبورد الصيدلي (Buyer)
        // GET /api/dashboard/buyer
        // ───────────────────────────────────────────
        [HttpGet("buyer")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> GetBuyerDashboard()
        {
            var userId = GetUserId();

            // المجموع الشهري
            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;

            var monthlySpend = await _context.Orders
                .Where(o => o.UserId == userId
                    && o.OrderDate.Month == currentMonth
                    && o.OrderDate.Year == currentYear
                    && o.Status != "Cancelled")
                .SumAsync(o => o.TotalAmount);

            // عدد الطلبات المفتوحة
            var openOrdersCount = await _context.Orders
                .Where(o => o.UserId == userId
                    && (o.Status == "Pending" || o.Status == "Processing"))
                .CountAsync();

            // تنبيهات المخزون المنخفض
            var inventoryAlerts = await _context.Medicines
                .Where(m => m.StockQuantity < 50)
                .CountAsync();

            // آخر 3 طلبات
            var recentOrders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Take(3)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.OrderDate,
                    o.Status,
                    o.TotalAmount,
                    TotalItems = o.OrderDetails.Sum(d => d.Quantity)
                })
                .ToListAsync();

            // Procurement Trends — آخر 6 أشهر
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var procurementTrends = await _context.Orders
                .Where(o => o.UserId == userId
                    && o.OrderDate >= sixMonthsAgo
                    && o.Status != "Cancelled")
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalSpend = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            return Ok(new
            {
                monthlySpend,
                openOrdersCount,
                inventoryAlerts,
                recentOrders,
                procurementTrends
            });
        }

        // ───────────────────────────────────────────
        // 2. داشبورد المورد/Admin (Supplier)
        // GET /api/dashboard/supplier
        // ───────────────────────────────────────────
        [HttpGet("supplier")]
        [Authorize(Roles = "Admin,PharmaceuticalCompany")]
        public async Task<IActionResult> GetSupplierDashboard()
        {
            // إجمالي الإيرادات
            var totalRevenue = await _context.Orders
                .Where(o => o.Status != "Cancelled")
                .SumAsync(o => o.TotalAmount);

            // الإيرادات الشهر الماضي للمقارنة
            var lastMonth = DateTime.UtcNow.AddMonths(-1);
            var lastMonthRevenue = await _context.Orders
                .Where(o => o.Status != "Cancelled"
                    && o.OrderDate.Month == lastMonth.Month
                    && o.OrderDate.Year == lastMonth.Year)
                .SumAsync(o => o.TotalAmount);

            // الطلبات المعلقة
            var pendingOrdersCount = await _context.Orders
                .Where(o => o.Status == "Pending")
                .CountAsync();

            // المنتجات تحت الحد الأدنى للمخزون
            var lowStockThreshold = 50;
            var lowStockBatches = await _context.Medicines
                .Where(m => m.StockQuantity < lowStockThreshold)
                .CountAsync();

            // تفاصيل المنتجات ذات المخزون المنخفض
            var lowStockItems = await _context.Medicines
                .AsNoTracking()
                .Where(m => m.StockQuantity < lowStockThreshold)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.SKU,
                    m.StockQuantity,
                    m.ImageUrl,
                    CategoryName = m.Category != null ? m.Category.Name : "General"
                })
                .Take(5)
                .ToListAsync();

            // Sales Analytics — آخر 6 أشهر
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var salesAnalytics = await _context.Orders
                .Where(o => o.OrderDate >= sixMonthsAgo
                    && o.Status != "Cancelled")
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            // آخر 4 طلبات واردة
            var recentOrders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .Take(4)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.OrderDate,
                    o.Status,
                    o.TotalAmount,
                    BuyerName = o.User.FullName,
                    BuyerOrg = o.User.OrganizationType
                })
                .ToListAsync();

            // نسبة نمو الإيرادات مقارنة بالشهر الماضي
            decimal revenueGrowth = 0;
            if (lastMonthRevenue > 0)
                revenueGrowth = ((totalRevenue - lastMonthRevenue) / lastMonthRevenue) * 100;

            return Ok(new
            {
                totalRevenue,
                revenueGrowth = Math.Round(revenueGrowth, 1),
                pendingOrdersCount,
                lowStockBatches,
                lowStockItems,
                salesAnalytics,
                recentOrders
            });
        }

        // ───────────────────────────────────────────
        // 3. إحصائيات سريعة للـ TopBar
        // GET /api/dashboard/stats
        // ───────────────────────────────────────────
        [HttpGet("stats")]
        public async Task<IActionResult> GetQuickStats()
        {
            var userId = GetUserId();
            var userRole = GetUserRole();

            if (userRole == "Pharmacist")
            {
                // عدد العناصر بالسلة
                var cartCount = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .SumAsync(c => c.Quantity);

                // عدد الطلبات المفتوحة
                var openOrders = await _context.Orders
                    .Where(o => o.UserId == userId
                        && (o.Status == "Pending" || o.Status == "Processing"))
                    .CountAsync();

                return Ok(new { cartCount, openOrders });
            }
            else
            {
                // للـ Admin/Supplier
                var pendingOrders = await _context.Orders
                    .Where(o => o.Status == "Pending")
                    .CountAsync();

                var lowStockCount = await _context.Medicines
                    .Where(m => m.StockQuantity < 50)
                    .CountAsync();

                return Ok(new { pendingOrders, lowStockCount });
            }
        }

        // ───────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }
    }
}