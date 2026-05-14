using Microsoft.AspNetCore.Mvc;
using E_commerce_system.Data;
using Microsoft.AspNetCore.Authorization;
using E_commerce_system.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace E_commerce_system.Controllers
{
    // [Authorize(Roles = "Admin")] // Uncomment when roles are seeded
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
            {
                context.Result = RedirectToAction("AdminLogin", "Account");
            }
            base.OnActionExecuting(context);
        }

        // 16. Admin Dashboard
        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                TotalRevenue = await _context.Orders
                    .Where(o => o.Status != null && o.Status.ToLower() != "cancelled")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0,
                    
                ActiveOrdersCount = await _context.Orders
                    .CountAsync(o => o.Status != null && o.Status.ToLower() != "delivered" && o.Status.ToLower() != "cancelled"),
                    
                OpenTicketsCount = await _context.SupportTickets
                    .CountAsync(t => t.Status != null && t.Status.ToLower() == "open"),
                    
                TotalProductsCount = await _context.Products.CountAsync(),
                TotalUsersCount = await _context.Users.CountAsync(u => u.Role != null && u.Role.ToLower() == "user"),
                
                RecentTickets = await _context.SupportTickets
                    .Include(t => t.User)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToListAsync(),
                    
                RecentOrders = await _context.Orders
                    .Include(o => o.User)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToListAsync()
            };
            return View(model);
        }

        // 17. Product Management
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products.Include(p => p.Category).ToListAsync();
            return View(products);
        }

        // Product Create
        [HttpGet]
        public async Task<IActionResult> ProductCreate()
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProductCreate(Product model, IFormFile imageFile)
        {
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    model.ImageUrl = "/images/products/" + uniqueFileName;
                }
                
                // Ensure CategoryId is 0 or null if not valid
                if (model.CategoryId <= 0) model.CategoryId = null;

                _context.Products.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction("Products");
            }
            return View(model);
        }

        // Product Edit
        [HttpGet]
        public async Task<IActionResult> ProductEdit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> ProductEdit(Product model, IFormFile? imageFile)
        {
            ModelState.Remove("Category");
            ModelState.Remove("imageFile");

            if (ModelState.IsValid)
            {
                var product = await _context.Products.FindAsync(model.Id);
                if (product == null) return NotFound();

                // Update only the fields from the form
                product.Name = model.Name;
                product.Description = model.Description;
                product.Price = model.Price;
                product.StockQuantity = model.StockQuantity;
                product.CategoryId = model.CategoryId > 0 ? model.CategoryId : null;
                product.Gender = model.Gender;

                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    product.ImageUrl = "/images/products/" + uniqueFileName;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Product updated successfully!";
                return RedirectToAction("Products");
            }

            // If we get here, something failed
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            TempData["Error"] = "Validation failed: " + string.Join(", ", errors);
            return View(model);
        }

        // Product Delete
        [HttpPost]
        public async Task<IActionResult> ProductDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Products");
        }

        // 18. Category Management
        public IActionResult Categories()
        {
            var categories = _context.Categories.ToList();
            return View(categories);
        }

        [HttpGet]
        public IActionResult CategoryCreate()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CategoryCreate(Category model, IFormFile imageFile)
        {
            ModelState.Remove("Products");
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "categories");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    model.ImageUrl = "/images/categories/" + uniqueFileName;
                }

                _context.Categories.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction("Categories");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult CategoryEdit(int id)
        {
            var category = _context.Categories.Find(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> CategoryEdit(Category model, IFormFile imageFile)
        {
            ModelState.Remove("Products");
            if (ModelState.IsValid)
            {
                var category = await _context.Categories.FindAsync(model.Id);
                if (category == null) return NotFound();

                category.Name = model.Name;

                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "categories");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    category.ImageUrl = "/images/categories/" + uniqueFileName;
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("Categories");
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CategoryDelete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Categories");
        }

        // 19. Order Management
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders.Include(t => t.User).OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = status;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        // 20. User Management
        public IActionResult Users()
        {
            var users = _context.Users
                .Where(u => u.Role == null || u.Role.ToLower() == "user")
                .ToList();
            return View(users);
        }

        // 21. Support Management Dashboard
        public async Task<IActionResult> Support()
        {
            var tickets = await _context.SupportTickets
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tickets);
        }

        // 21. Admin Chat Screen
        public async Task<IActionResult> SupportChat(int id)
        {
            var ticket = await _context.SupportTickets
                .Include(t => t.Messages)
                .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(t => t.Id == id);
            
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        // Action to send a reply from the admin
        [HttpPost]
        public async Task<IActionResult> ReplyToTicket(int ticketId, string message)
        {
            var adminId = HttpContext.Session.GetInt32("UserId");
            if (adminId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(message)) return RedirectToAction("SupportChat", new { id = ticketId });

            var ticket = await _context.SupportTickets.FindAsync(ticketId);
            if (ticket == null) return NotFound();

            var chatMessage = new ChatMessage
            {
                TicketId = ticketId,
                SenderId = adminId.Value,
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsAdminReply = true
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            return RedirectToAction("SupportChat", new { id = ticketId });
        }
        // Action to close a support ticket
        [HttpPost]
        public async Task<IActionResult> CloseTicket(int ticketId)
        {
            var adminId = HttpContext.Session.GetInt32("UserId");
            if (adminId == null) return Unauthorized();

            var ticket = await _context.SupportTickets.FindAsync(ticketId);
            if (ticket == null) return NotFound();

            ticket.Status = "Closed";
            await _context.SaveChangesAsync();

            return RedirectToAction("SupportChat", new { id = ticketId });
        }
        // Flash Sale Management
        public async Task<IActionResult> FlashSales()
        {
            var products = await _context.Products.ToListAsync();
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> SetFlashSale(int productId, bool isFlashSale, int discountPercentage, DateTime? flashSaleEndTime)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            product.IsFlashSale = isFlashSale;
            product.DiscountPercentage = isFlashSale ? discountPercentage : 0;
            product.FlashSaleEndTime = isFlashSale ? flashSaleEndTime : null;

            await _context.SaveChangesAsync();
            return RedirectToAction("FlashSales");
        }

        // Banner Management
        public async Task<IActionResult> Banners()
        {
            var banners = await _context.Banners.OrderBy(b => b.DisplayOrder).ToListAsync();
            return View(banners);
        }

        [HttpGet]
        public IActionResult BannerCreate()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> BannerCreate(Banner model, IFormFile imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "banners");
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }
                model.ImageUrl = "/images/banners/" + uniqueFileName;
            }
            else
            {
                ModelState.AddModelError("imageFile", "Image is required");
            }

            // Remove ImageUrl from validation because we set it manually after file upload
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                _context.Banners.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction("Banners");
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> BannerEdit(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner == null) return NotFound();
            return View(banner);
        }

        [HttpPost]
        public async Task<IActionResult> BannerEdit(Banner model, IFormFile? imageFile)
        {
            var banner = await _context.Banners.FindAsync(model.Id);
            if (banner == null) return NotFound();

            banner.Title = model.Title;
            banner.Subtitle = model.Subtitle;
            banner.TargetUrl = model.TargetUrl;
            banner.DisplayOrder = model.DisplayOrder;
            banner.IsActive = model.IsActive;

            if (imageFile != null && imageFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "banners");
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }
                banner.ImageUrl = "/images/banners/" + uniqueFileName;
            }

            // Remove ImageUrl from validation because it's handled manually
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                await _context.SaveChangesAsync();
                return RedirectToAction("Banners");
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> BannerDelete(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner != null)
            {
                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Banners");
        }
    }
}
