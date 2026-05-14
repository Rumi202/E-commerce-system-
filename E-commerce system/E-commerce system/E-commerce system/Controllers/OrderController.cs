using Microsoft.AspNetCore.Mvc;
using E_commerce_system.Data;
using E_commerce_system.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text.Json.Nodes;
using SelectPdf;
namespace E_commerce_system.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private const string CartSessionKey = "CartItems";

        public OrderController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                context.Result = RedirectToAction("Index", "Admin");
            }
            base.OnActionExecuting(context);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(string ShippingAddress, string PaymentMethod)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(cartJson))
            {
                return RedirectToAction("Index", "Cart");
            }

            var cartItems = JsonSerializer.Deserialize<List<CartItem>>(cartJson);
            if (cartItems == null || !cartItems.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            var order = new Order
            {
                UserId = userId.Value,
                OrderDate = DateTime.Now,
                Status = "Pending",
                TotalAmount = cartItems.Sum(i => i.TotalPrice),
                ShippingAddress = ShippingAddress ?? "Address not provided",
                PaymentMethod = PaymentMethod ?? "Credit Card",
                OrderItems = new List<OrderItem>()
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in cartItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.StockQuantity < item.Quantity)
                    {
                        TempData["Error"] = $"Sorry, {item.ProductName} is out of stock or has insufficient quantity.";
                        return RedirectToAction("Index", "Cart");
                    }

                    // Reduce stock
                    product.StockQuantity -= item.Quantity;

                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "An error occurred while processing your order. Please try again.";
                return RedirectToAction("Index", "Cart");
            }

            // Clear the cart
            HttpContext.Session.Remove(CartSessionKey);

            return RedirectToAction("PaymentSelection", new { id = order.Id });
        }

        // Payment Selection Page
        public async Task<IActionResult> PaymentSelection(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null || order.Status != "Pending") return NotFound();

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> PayWithCOD(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (order == null) return NotFound();

            order.PaymentMethod = "Cash on Delivery";
            order.Status = "Confirmed";
            await _context.SaveChangesAsync();

            return RedirectToAction("Success", new { id = order.Id });
        }

        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> InitSSLCommerz(int orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { status = "FAILED", data = "Unauthorized" });

            var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return Json(new { status = "FAILED", data = "Order not found" });

            string storeId = _configuration["SSLCommerz:StoreId"];
            string storePass = _configuration["SSLCommerz:StorePassword"];
            bool isSandbox = _configuration.GetValue<bool>("SSLCommerz:IsSandbox");
            string apiUrl = isSandbox ? "https://sandbox.sslcommerz.com/gwprocess/v4/api.php" : "https://securepay.sslcommerz.com/gwprocess/v4/api.php";
            
            var requestHost = $"{Request.Scheme}://{Request.Host}";

            var postData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("store_id", storeId ?? ""),
                new KeyValuePair<string, string>("store_passwd", storePass ?? ""),
                new KeyValuePair<string, string>("total_amount", order.TotalAmount.ToString("0.00")),
                new KeyValuePair<string, string>("currency", "BDT"),
                new KeyValuePair<string, string>("tran_id", order.Id.ToString()),
                new KeyValuePair<string, string>("success_url", $"{requestHost}/Order/SSLCommerzSuccess"),
                new KeyValuePair<string, string>("fail_url", $"{requestHost}/Order/SSLCommerzFail"),
                new KeyValuePair<string, string>("cancel_url", $"{requestHost}/Order/SSLCommerzCancel"),
                new KeyValuePair<string, string>("cus_name", order.User?.Name ?? "Guest User"),
                new KeyValuePair<string, string>("cus_email", order.User?.Email ?? "guest@example.com"),
                new KeyValuePair<string, string>("cus_add1", order.ShippingAddress ?? "Dhaka"),
                new KeyValuePair<string, string>("cus_city", "Dhaka"),
                new KeyValuePair<string, string>("cus_country", "Bangladesh"),
                new KeyValuePair<string, string>("cus_phone", "01700000000"), // Add proper phone if available
                new KeyValuePair<string, string>("shipping_method", "NO"),
                new KeyValuePair<string, string>("product_name", "E-commerce System Order"),
                new KeyValuePair<string, string>("product_category", "General"),
                new KeyValuePair<string, string>("product_profile", "general")
            });

            using var client = new HttpClient();
            var response = await client.PostAsync(apiUrl, postData);
            var responseString = await response.Content.ReadAsStringAsync();

            try 
            {
                var jsonNode = JsonNode.Parse(responseString);
                var status = jsonNode?["status"]?.ToString();
                
                if (status?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var gatewayUrl = jsonNode?["GatewayPageURL"]?.ToString();
                    return Json(new { status = "success", data = gatewayUrl });
                }
                else
                {
                    var failReason = jsonNode?["failedreason"]?.ToString() ?? "Unknown error";
                    return Json(new { status = "FAILED", data = failReason, raw = responseString });
                }
            }
            catch
            {
                return Json(new { status = "FAILED", data = "Error parsing response", raw = responseString });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken] // SSLCommerz POSTs back without an antiforgery token
        public async Task<IActionResult> SSLCommerzSuccess([FromForm] IFormCollection form)
        {
            var tranId = form["tran_id"].ToString();
            var status = form["status"].ToString();
            
            if (int.TryParse(tranId, out int orderId) && status == "VALID")
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    order.PaymentMethod = "SSLCommerz";
                    order.Status = "Confirmed";
                    
                    // Note: In production, call the Order Validation API to verify this transaction is valid and not spoofed!
                    
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Success", new { id = order.Id });
                }
            }
            
            TempData["Error"] = "Payment verification failed.";
            return RedirectToAction("History");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SSLCommerzFail([FromForm] IFormCollection form)
        {
            TempData["Error"] = "Payment failed. Please try again.";
            var tranId = form["tran_id"].ToString();
            if (int.TryParse(tranId, out int orderId)) {
                return RedirectToAction("PaymentSelection", new { id = orderId });
            }
            return RedirectToAction("History");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SSLCommerzCancel([FromForm] IFormCollection form)
        {
            TempData["Error"] = "Payment was cancelled.";
            var tranId = form["tran_id"].ToString();
            if (int.TryParse(tranId, out int orderId)) {
                return RedirectToAction("PaymentSelection", new { id = orderId });
            }
            return RedirectToAction("History");
        }

        // 7. Order Success Page
        public async Task<IActionResult> Success(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            return View(order);
        }

        // 10. Order History Page
        public async Task<IActionResult> History()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // 11. Order Details Page
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadInvoice(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            var html = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: 'Helvetica Neue', 'Helvetica', Arial, sans-serif; padding: 40px; color: #333; }}
                    .header {{ border-bottom: 2px solid #000; padding-bottom: 20px; margin-bottom: 30px; }}
                    .logo {{ font-size: 32px; font-weight: bold; color: #000; }}
                    .company-info {{ margin-top: 10px; color: #666; font-size: 14px; line-height: 1.5; }}
                    .invoice-info {{ float: right; text-align: right; }}
                    .invoice-title {{ font-size: 28px; color: #444; letter-spacing: 2px; }}
                    .clear {{ clear: both; }}
                    .details {{ margin-bottom: 40px; }}
                    .details-box {{ float: left; width: 45%; }}
                    .details-box.right {{ float: right; text-align: right; }}
                    .details strong {{ display: block; margin-bottom: 8px; font-size: 14px; text-transform: uppercase; color: #888; letter-spacing: 1px; }}
                    .details div {{ font-size: 15px; margin-bottom: 4px; }}
                    .table {{ width: 100%; border-collapse: collapse; margin-bottom: 30px; margin-top: 20px; }}
                    .table th, .table td {{ padding: 15px 10px; border-bottom: 1px solid #eaeaea; text-align: left; }}
                    .table th {{ background: #f8f9fa; font-weight: bold; font-size: 14px; text-transform: uppercase; color: #555; border-bottom: 2px solid #ddd; }}
                    .table td {{ font-size: 15px; }}
                    .total-row {{ font-size: 18px; font-weight: bold; }}
                    .total-row td {{ padding-top: 20px; border-top: 2px solid #000; border-bottom: none; }}
                    .footer {{ margin-top: 60px; text-align: center; color: #888; font-size: 13px; border-top: 1px solid #eaeaea; padding-top: 20px; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <div class='invoice-info'>
                        <div class='invoice-title'>INVOICE</div>
                        <div style='font-size:16px; margin-top:5px; font-weight: bold; color: #000;'>#{order.Id:D6}</div>
                        <div style='font-size:14px; margin-top:10px; color: #666;'>Date: {order.OrderDate.ToString("dd MMM yyyy")}</div>
                    </div>
                    <div>
                        <div class='logo'>RN Store</div>
                        <div class='company-info'>
                            123 Commerce Avenue<br/>
                            Dhaka, Bangladesh 1200<br/>
                            support@rnstore.com | +880 1700-000000
                        </div>
                    </div>
                    <div class='clear'></div>
                </div>

                <div class='details'>
                    <div class='details-box'>
                        <strong>Billed To:</strong>
                        <div style='font-weight: bold; color: #000;'>{order.User?.Name ?? "Customer"}</div>
                        <div>{order.User?.Email}</div>
                        <div style='margin-top: 5px; color: #555;'>{order.ShippingAddress}</div>
                    </div>
                    <div class='details-box right'>
                        <strong>Payment Information:</strong>
                        <div>Method: <span style='font-weight: bold;'>{order.PaymentMethod}</span></div>
                        <div>Status: <span style='color: {(order.Status == "Confirmed" ? "#10b981" : "#f59e0b")}; font-weight: bold;'>{order.Status}</span></div>
                    </div>
                    <div class='clear'></div>
                </div>

                <table class='table'>
                    <thead>
                        <tr>
                            <th>Item Description</th>
                            <th style='text-align:center;'>Qty</th>
                            <th style='text-align:right;'>Price</th>
                            <th style='text-align:right;'>Amount</th>
                        </tr>
                    </thead>
                    <tbody>";

            foreach (var item in order.OrderItems)
            {
                html += $@"
                        <tr>
                            <td>{item.Product?.Name}</td>
                            <td style='text-align:center;'>{item.Quantity}</td>
                            <td style='text-align:right;'>BDT {item.UnitPrice.ToString("N2")}</td>
                            <td style='text-align:right; font-weight: 500;'>BDT {(item.Quantity * item.UnitPrice).ToString("N2")}</td>
                        </tr>";
            }

            html += $@"
                        <tr class='total-row'>
                            <td colspan='3' style='text-align:right;'>Total Amount:</td>
                            <td style='text-align:right; color: #000;'>BDT {order.TotalAmount.ToString("N2")}</td>
                        </tr>
                    </tbody>
                </table>

                <div class='footer'>
                    <strong>Thank you for your business!</strong><br/>
                    If you have any questions regarding this invoice, please contact our support team.
                </div>
            </body>
            </html>";

            try
            {
                var converter = new HtmlToPdf();
                
                // Configure PDF options
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginLeft = 20;
                converter.Options.MarginRight = 20;
                converter.Options.MarginTop = 30;
                converter.Options.MarginBottom = 30;

                // SelectPdf free version has a 5-page limit, which is plenty for an invoice.
                PdfDocument doc = converter.ConvertHtmlString(html);
                
                byte[] pdfBytes = doc.Save();
                doc.Close();

                return File(pdfBytes, "application/pdf", $"RN_Store_Invoice_{order.Id:D6}.pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not generate PDF invoice at this time.";
                return RedirectToAction("Details", new { id = order.Id });
            }
        }
    }
}
