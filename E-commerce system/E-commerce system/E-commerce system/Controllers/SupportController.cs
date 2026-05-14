using Microsoft.AspNetCore.Mvc;
using E_commerce_system.Data;
using E_commerce_system.Models;
using Microsoft.EntityFrameworkCore;

namespace E_commerce_system.Controllers
{
    public class SupportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SupportController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                context.Result = RedirectToAction("Index", "Admin");
            }
            base.OnActionExecuting(context);
        }

        // 12. Support Chat List Page
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var tickets = _context.SupportTickets
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
            return View(tickets);
        }

        // 13. Chat / Support Conversation Screen
        public async Task<IActionResult> Chat(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var ticket = await _context.SupportTickets
                .Include(t => t.Messages)
                .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (ticket == null) return NotFound();
            return View(ticket);
        }

        // Action to send a message from the user
        [HttpPost]
        public async Task<IActionResult> SendMessage(int ticketId, string message)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(message)) return RedirectToAction("Chat", new { id = ticketId });

            var ticket = await _context.SupportTickets.FindAsync(ticketId);
            if (ticket == null || ticket.UserId != userId) return NotFound();

            var chatMessage = new ChatMessage
            {
                TicketId = ticketId,
                SenderId = userId.Value,
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsAdminReply = false
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            return RedirectToAction("Chat", new { id = ticketId });
        }

        // 14. Create Support Ticket Page
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(SupportTicket model, IFormFile? ticketImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            ModelState.Remove("User");
            ModelState.Remove("Messages");
            ModelState.Remove("Status");

            if (ModelState.IsValid)
            {
                if (ticketImage != null && ticketImage.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "support");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + ticketImage.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ticketImage.CopyToAsync(fileStream);
                    }
                    model.ImageUrl = "/images/support/" + uniqueFileName;
                }

                model.UserId = userId.Value;
                model.Status = "Open";
                model.CreatedAt = DateTime.UtcNow;
                _context.SupportTickets.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction("Chat", new { id = model.Id });
            }
            return View(model);
        }

        // 15. Support Ticket Details Page
        public IActionResult Details(int id)
        {
            var ticket = _context.SupportTickets.Find(id);
            return View(ticket);
        }
        // Action to delete a message
        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var message = await _context.ChatMessages
                .Include(m => m.Ticket)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) return NotFound();

            // Only allow the user to delete their own messages
            if (message.SenderId != userId.Value || message.IsAdminReply)
            {
                return Forbid();
            }

            int ticketId = message.TicketId;
            _context.ChatMessages.Remove(message);
            await _context.SaveChangesAsync();

            return RedirectToAction("Chat", new { id = ticketId });
        }
        // Action to delete a ticket
        [HttpPost]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var ticket = await _context.SupportTickets
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (ticket == null) return NotFound();

            // Also delete all messages associated with this ticket
            var messages = _context.ChatMessages.Where(m => m.TicketId == id);
            _context.ChatMessages.RemoveRange(messages);
            
            _context.SupportTickets.Remove(ticket);
            await _context.SaveChangesAsync();

            return RedirectToAction("Profile", "Account");
        }
    }
}
