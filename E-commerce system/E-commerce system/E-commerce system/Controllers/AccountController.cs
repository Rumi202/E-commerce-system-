using Microsoft.AspNetCore.Mvc;
using E_commerce_system.Models;
using E_commerce_system.Data;
using Microsoft.EntityFrameworkCore;
using E_commerce_system.Services;

namespace E_commerce_system.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AccountController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // 8. Login Page
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            // Simple string matching for demonstration
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.PasswordHash == password);
            if (user != null)
            {
                if (user.IsBlocked)
                {
                    ModelState.AddModelError("", "Your account has been blocked. Please contact support.");
                    return View();
                }

                if (!user.IsEmailVerified)
                {
                    return RedirectToAction("VerifyEmailInfo", new { email = user.Email, token = user.EmailVerificationToken });
                }

                HttpContext.Session.SetString("UserName", user.Name ?? user.Email ?? "User");
                HttpContext.Session.SetString("UserRole", user.Role ?? "User");
                HttpContext.Session.SetInt32("UserId", user.Id);

                if (user.Role == "Admin")
                {
                    return RedirectToAction("Index", "Admin");
                }
                return RedirectToAction("Profile", "Account");
            }
            ModelState.AddModelError("", "Invalid credentials");
            return View();
        }

        [HttpGet]
        public IActionResult AdminLogin()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AdminLogin(string email, string password)
        {
            // Simple string matching for demonstration - looking for Admin role specifically
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.PasswordHash == password && u.Role == "Admin");
            if (user != null)
            {
                if (user.IsBlocked)
                {
                    ModelState.AddModelError("", "This admin account is currently suspended. Please contact the system owner.");
                    return View();
                }

                HttpContext.Session.SetString("UserName", user.Name ?? "Admin");
                HttpContext.Session.SetString("UserRole", "Admin");
                HttpContext.Session.SetInt32("UserId", user.Id);
                return RedirectToAction("Index", "Admin");
            }
            ModelState.AddModelError("", "Invalid admin credentials or unauthorized access.");
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // 8. Register Page
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model)
        {
            // The Role property is implicitly required due to nullable reference types. We remove the error since we set it manually.
            ModelState.Remove("Role");

            if (ModelState.IsValid)
            {
                string verificationCode = new Random().Next(100000, 999999).ToString();
                model.Role = "User";
                model.IsEmailVerified = false;
                model.EmailVerificationToken = verificationCode;
                
                _context.Users.Add(model);
                await _context.SaveChangesAsync();
                
                // Dynamic Email Verification Code
                var subject = "Your Verification Code - RN Store";
                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px; text-align: center;'>
                        <h2 style='color: #333;'>Welcome to RN Store!</h2>
                        <p>Thank you for registering. Your verification code is:</p>
                        <div style='background: #f4f4f4; padding: 20px; font-size: 32px; font-weight: bold; letter-spacing: 10px; color: #000; margin: 20px 0; border-radius: 5px;'>
                            {verificationCode}
                        </div>
                        <p>Please enter this code on the verification page to activate your account.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                        <p style='font-size: 12px; color: #777;'>If you didn't create an account, you can safely ignore this email.</p>
                    </div>";

                try {
                    await _emailService.SendEmailAsync(model.Email, subject, body);
                } catch (Exception ex) {
                    // Log error but don't break the registration flow for now
                    Console.WriteLine($"Email send failed: {ex.Message}");
                }
                
                return RedirectToAction("VerifyEmailInfo", new { email = model.Email, token = model.EmailVerificationToken });
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyEmailInfo(string email, string token)
        {
            ViewBag.Email = email;
            ViewBag.Token = token; // Including token for easy testing/simulation
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyEmail(string email, string code)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.EmailVerificationToken == code);
            if (user == null)
            {
                ViewBag.Error = "Invalid verification code. Please try again.";
                ViewBag.Email = email;
                return View("VerifyEmailInfo");
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return View("VerificationSuccess");
        }

        [HttpPost]
        public async Task<IActionResult> ResendVerificationCode(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || user.IsEmailVerified)
            {
                return RedirectToAction("Login");
            }

            string newCode = new Random().Next(100000, 999999).ToString();
            user.EmailVerificationToken = newCode;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var subject = "Your New Verification Code - RN Store";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px; text-align: center;'>
                    <h2 style='color: #333;'>New Verification Code</h2>
                    <p>You requested a new verification code. Your code is:</p>
                    <div style='background: #f4f4f4; padding: 20px; font-size: 32px; font-weight: bold; letter-spacing: 10px; color: #000; margin: 20px 0; border-radius: 5px;'>
                        {newCode}
                    </div>
                    <p>Please enter this code on the verification page to activate your account.</p>
                </div>";

            try {
                await _emailService.SendEmailAsync(user.Email, subject, body);
                ViewBag.Message = "A new verification code has been sent to your email.";
            } catch {
                ViewBag.Error = "Failed to send email. Please check your SMTP settings.";
            }

            ViewBag.Email = email;
            ViewBag.Token = newCode;
            return View("VerifyEmailInfo");
        }

        // 8. Forgot Password
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // 9. User Profile Page
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            // Fetch orders and tickets separately to avoid complex joins if not needed, 
            // but the view also needs them. I'll use ViewBag for simplicity since the model is User.
            ViewBag.Orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Tickets = await _context.SupportTickets
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Profile(User model, IFormFile? profileImage)
        {
            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Update allowed fields
            user.Name = model.Name;
            user.Email = model.Email;
            user.Address = model.Address;

            if (profileImage != null && profileImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profiles");
                Directory.CreateDirectory(uploadsFolder);
                
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + profileImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(fileStream);
                }
                
                user.ProfilePictureUrl = "/images/profiles/" + uniqueFileName;
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update session if name changed
            HttpContext.Session.SetString("UserName", user.Name);
            
            ViewBag.Message = "Profile updated successfully!";
            return View(user);
        }
    }
}
