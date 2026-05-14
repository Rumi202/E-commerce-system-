using Microsoft.AspNetCore.Mvc;
using E_commerce_system.Data;
using Microsoft.EntityFrameworkCore;

namespace E_commerce_system.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                context.Result = RedirectToAction("Index", "Admin");
            }
            base.OnActionExecuting(context);
        }

        // 3. Product Listing Page (Shop)
        public async Task<IActionResult> Index(int? categoryId, string? gender, int page = 1)
        {
            int pageSize = 12;
            var productsQuery = _context.Products.AsQueryable();
            
            string? selectedCategoryName = null;

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
                var category = await _context.Categories.FindAsync(categoryId.Value);
                selectedCategoryName = category?.Name;
            }

            if (!string.IsNullOrEmpty(gender))
            {
                productsQuery = productsQuery.Where(p => p.Gender == gender);
            }

            int totalProducts = await productsQuery.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalProducts / pageSize);

            var pagedProducts = await productsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new E_commerce_system.Models.ProductListViewModel
            {
                Products = pagedProducts,
                Categories = await _context.Categories.ToListAsync(),
                SelectedCategoryId = categoryId,
                SelectedCategoryName = selectedCategoryName,
                SelectedGender = gender,
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(vm);
        }

        // 4. Product Details Page
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }
    }
}
