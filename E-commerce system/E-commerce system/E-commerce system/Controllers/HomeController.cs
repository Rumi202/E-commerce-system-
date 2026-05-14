using Microsoft.AspNetCore.Mvc;
using E_commerce_system.Data;
using Microsoft.EntityFrameworkCore;
using E_commerce_system.Models;

namespace E_commerce_system.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                context.Result = RedirectToAction("Index", "Admin");
            }
            base.OnActionExecuting(context);
        }

        // 1. Splash / Landing Page
        public IActionResult Splash()
        {
            return View();
        }

        // 2. Home Page
        public async Task<IActionResult> Index(int? categoryId, string? gender, int page = 1)
        {
            int pageSize = 12;
            var productsQuery = _context.Products.AsQueryable();
            
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
                ViewBag.SelectedCategoryId = categoryId.Value;
                var category = await _context.Categories.FindAsync(categoryId.Value);
                ViewBag.SelectedCategoryName = category?.Name;
            }

            if (!string.IsNullOrEmpty(gender))
            {
                productsQuery = productsQuery.Where(p => p.Gender == gender);
                ViewBag.SelectedGender = gender;
            }

            int totalProducts = await productsQuery.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalProducts / pageSize);

            var pagedProducts = await productsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Fetch best-selling products by total quantity sold
            var bestSellingProductIds = await _context.OrderItems
                .GroupBy(oi => oi.ProductId)
                .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(oi => oi.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Take(8)
                .Select(x => x.ProductId)
                .ToListAsync();

            List<Product> bestSellingProducts;
            if (bestSellingProductIds.Any())
            {
                // Fetch the products and preserve the sales ranking order
                var productsQueryBS = _context.Products.AsQueryable();
                if (categoryId.HasValue)
                {
                    productsQueryBS = productsQueryBS.Where(p => p.CategoryId == categoryId.Value);
                }

                var productsDict = await productsQueryBS
                    .Where(p => bestSellingProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);
                bestSellingProducts = bestSellingProductIds
                    .Where(id => productsDict.ContainsKey(id))
                    .Select(id => productsDict[id])
                    .ToList();
            }
            else
            {
                // Fallback: show latest products if no orders exist yet
                var productsQueryFB = _context.Products.AsQueryable();
                if (categoryId.HasValue)
                {
                    productsQueryFB = productsQueryFB.Where(p => p.CategoryId == categoryId.Value);
                }

                bestSellingProducts = await productsQueryFB
                    .OrderByDescending(p => p.Id)
                    .Take(8)
                    .ToListAsync();
            }

            // Fetch Active Flash Sales
            var flashSaleQuery = _context.Products
                .Where(p => p.IsFlashSale && p.FlashSaleEndTime > DateTime.UtcNow);
            
            if (categoryId.HasValue)
            {
                flashSaleQuery = flashSaleQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            var flashSaleProducts = await flashSaleQuery
                .OrderBy(p => p.FlashSaleEndTime)
                .Take(4)
                .ToListAsync();

            DateTime? globalFlashSaleEndTime = flashSaleProducts.Any() 
                ? flashSaleProducts.Min(p => p.FlashSaleEndTime) 
                : null;

            var vm = new HomeViewModel
            {
                Products = pagedProducts,
                Categories = await _context.Categories.ToListAsync(),
                BestSellingProducts = bestSellingProducts,
                FlashSaleProducts = flashSaleProducts,
                Banners = await _context.Banners.Where(b => b.IsActive).OrderBy(b => b.DisplayOrder).ToListAsync(),
                GlobalFlashSaleEndTime = globalFlashSaleEndTime,
                SelectedGender = gender,
                CurrentPage = page,
                TotalPages = totalPages
            };
            return View(vm);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
