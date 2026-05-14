using Microsoft.AspNetCore.Mvc;
using E_commerce_system.Models;
using E_commerce_system.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace E_commerce_system.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string CartSessionKey = "CartItems";

        public CartController(ApplicationDbContext context)
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

        public async Task<IActionResult> Index()
        {
            var cart = GetCartFromSession();
            
            // Refresh stock quantities and prices from database
            foreach (var item in cart)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    item.StockQuantity = product.StockQuantity;
                    
                    // Update discount info dynamically
                    bool isActiveFlashSale = product.IsFlashSale && product.FlashSaleEndTime > DateTime.UtcNow;
                    item.IsFlashSale = isActiveFlashSale;
                    item.DiscountPercentage = isActiveFlashSale ? product.DiscountPercentage : 0;
                    item.OriginalPrice = product.Price;
                    item.UnitPrice = isActiveFlashSale ? product.DiscountedPrice : product.Price;
                }
            }
            
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            var cart = GetCartFromSession();
            var cartItem = cart.FirstOrDefault(c => c.ProductId == id);

            if (cartItem != null)
            {
                if (cartItem.Quantity + 1 > product.StockQuantity)
                {
                    TempData["Error"] = $"Cannot add more than available stock ({product.StockQuantity}).";
                    return RedirectToAction("Index");
                }
                cartItem.Quantity++;
            }
            else
            {
                if (product.StockQuantity <= 0)
                {
                    TempData["Error"] = "Product is out of stock.";
                    return RedirectToAction("Index");
                }
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.DiscountedPrice,
                    OriginalPrice = product.Price,
                    DiscountPercentage = (product.IsFlashSale && product.FlashSaleEndTime > DateTime.UtcNow) ? product.DiscountPercentage : 0,
                    IsFlashSale = product.IsFlashSale && product.FlashSaleEndTime > DateTime.UtcNow,
                    Quantity = 1,
                    StockQuantity = product.StockQuantity,
                    ImageUrl = product.ImageUrl
                });
            }

            SaveCartToSession(cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int id)
        {
            var cart = GetCartFromSession();
            var itemToRemove = cart.FirstOrDefault(c => c.ProductId == id);
            
            if (itemToRemove != null)
            {
                cart.Remove(itemToRemove);
            }

            SaveCartToSession(cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int id, int quantity)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            var cart = GetCartFromSession();
            var cartItem = cart.FirstOrDefault(c => c.ProductId == id);

            if (cartItem != null && quantity > 0)
            {
                if (quantity > product.StockQuantity)
                {
                    cartItem.Quantity = product.StockQuantity;
                    TempData["Error"] = $"Quantity capped at available stock ({product.StockQuantity}).";
                }
                else
                {
                    cartItem.Quantity = quantity;
                }
            }
            else if (cartItem != null && quantity <= 0)
            {
                cart.Remove(cartItem);
            }

            SaveCartToSession(cart);
            return RedirectToAction("Index");
        }

        // 6. Checkout Page
        public IActionResult Checkout()
        {
            var cart = GetCartFromSession();
            if (!cart.Any()) return RedirectToAction("Index");
            return View();
        }

        private List<CartItem> GetCartFromSession()
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            return cartJson == null ? new List<CartItem>() : JsonSerializer.Deserialize<List<CartItem>>(cartJson);
        }

        private void SaveCartToSession(List<CartItem> cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CartSessionKey, cartJson);
        }
    }
}
