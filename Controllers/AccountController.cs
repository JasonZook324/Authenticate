using Microsoft.AspNetCore.Mvc;
using Authenticate.Models;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.WebUtilities;
using Authenticate.Infrastructure.Email;

namespace Authenticate.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;

        public AccountController(ApplicationDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }

        // GET /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        // POST /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var emailLower = model.Email.Trim().ToLowerInvariant();
            var emailEntity = await _db.Emails
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.EmailAddress != null && e.EmailAddress.ToLower() == emailLower);

            var genericMsg = "If an account with that email exists, a password reset link has been sent.";
            if (emailEntity?.User is not null && emailEntity.User.IsActive)
            {
                var tokenBytes = RandomNumberGenerator.GetBytes(32);
                var token = WebEncoders.Base64UrlEncode(tokenBytes);

                var reset = new PasswordReset
                {
                    Token = token,
                    UserId = emailEntity.User.Id,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
                };
                _db.PasswordResets.Add(reset);
                await _db.SaveChangesAsync();

                var callback = Url.Action(nameof(ResetPassword), "Account", new { token }, Request.Scheme) ?? "#";

                var subject = "Reset your password";
                var html = $@"
                    <p>We received a request to reset your password.</p>
                    <p><a href=""{callback}"">Click here to reset your password</a></p>
                    <p>If you did not request this, you can ignore this email.</p>";
                var text = $"Reset your password: {callback}";

                try
                {
                    await _emailSender.SendAsync(emailEntity.EmailAddress!, subject, html, text);
                }
                catch (Exception)
                {
                    // Do not leak failures to the user (avoid account enumeration). Log if desired.
                }
            }

            TempData["Message"] = genericMsg;
            TempData["MessageClass"] = "alert-info";
            return RedirectToAction(nameof(Login));
        }

        // GET /Account/ResetPassword?token=...
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Message"] = "Invalid password reset link.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction(nameof(Login));
            }

            var reset = await _db.PasswordResets
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == token);

            if (reset is null || reset.UsedAtUtc != null || reset.ExpiresAtUtc < DateTime.UtcNow || reset.User == null || !reset.User.IsActive)
            {
                TempData["Message"] = "This password reset link is invalid or has expired.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction(nameof(Login));
            }

            return View(new ResetPasswordViewModel { Token = token });
        }

        // POST /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var reset = await _db.PasswordResets
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == model.Token);

            if (reset is null || reset.UsedAtUtc != null || reset.ExpiresAtUtc < DateTime.UtcNow || reset.User == null || !reset.User.IsActive)
            {
                TempData["Message"] = "This password reset link is invalid or has expired.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction(nameof(Login));
            }

            // Hash and upsert password
            var newHash = HashPassword(model.Password);
            var pwd = await _db.Passwords.FirstOrDefaultAsync(p => p.UserId == reset.UserId);
            if (pwd is null)
            {
                pwd = new Password { UserId = reset.UserId, PasswordHash = newHash };
                _db.Passwords.Add(pwd);
            }
            else
            {
                pwd.PasswordHash = newHash;
            }

            // Invalidate token
            reset.UsedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Message"] = "Your password has been reset. Please sign in.";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(nameof(Login));
        }

        // Save or update Person for the current user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePerson(int? id, string firstName, string lastName)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToAction(nameof(Login));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
                return RedirectToAction(nameof(Login));

            firstName = (firstName ?? string.Empty).Trim();
            lastName = (lastName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                TempData["Message"] = "First and Last name are required.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction(nameof(Account));
            }

            Person? person = null;
            if (id.HasValue && id.Value > 0)
                person = await _db.Set<Person>().FirstOrDefaultAsync(p => p.Id == id.Value && p.UserId == userId);

            person ??= await _db.Set<Person>().FirstOrDefaultAsync(p => p.UserId == userId);

            if (person == null)
            {
                person = new Person
                {
                    FirstName = firstName,
                    LastName = lastName,
                    UserId = userId
                };
                _db.Set<Person>().Add(person);
            }
            else
            {
                person.FirstName = firstName;
                person.LastName = lastName;
            }

            await _db.SaveChangesAsync();

            TempData["Message"] = "Profile saved.";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(nameof(Account));
        }

        // Save or update Email for the current user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEmail(string email)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToAction(nameof(Login));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
                return RedirectToAction(nameof(Login));

            email = (email ?? string.Empty).Trim();

            // Basic validation
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            {
                TempData["Message"] = "Please enter a valid email address.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction(nameof(Account));
            }

            var emailLower = email.ToLowerInvariant();

            // Ensure uniqueness across all users (case-insensitive), excluding current user's existing email
            var emailInUseByAnother = await _db.Emails
                .Include(e => e.User)
                .AnyAsync(e => e.EmailAddress != null
                               && e.EmailAddress.ToLower() == emailLower
                               && e.User != null
                               && e.User.Id != userId);

            if (emailInUseByAnother)
            {
                TempData["Message"] = "That email address is already registered.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction(nameof(Account));
            }

            // Load current user and their email (first one if multiple)
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                TempData["Message"] = "User not found.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction(nameof(Account));
            }

            var currentEmail = await _db.Emails
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.User != null && e.User.Id == userId);

            if (currentEmail == null)
            {
                currentEmail = new Email
                {
                    EmailAddress = email,
                    IsVerified = false,
                    User = user
                };
                _db.Emails.Add(currentEmail);
            }
            else
            {
                // If email changed, reset verification
                var changed = !string.Equals(currentEmail.EmailAddress, email, StringComparison.OrdinalIgnoreCase);
                currentEmail.EmailAddress = email;
                if (changed)
                    currentEmail.IsVerified = false;
            }

            await _db.SaveChangesAsync();

            TempData["Message"] = "Email saved.";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(nameof(Account));
        }

        // GET /Account/Account
        [HttpGet]
        public IActionResult Account() => View();

        // GET /Account/Login
        [HttpGet]
        public IActionResult Login() => View();

        // POST /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            var input = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View();
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == input);
            if (user == null)
            {
                var emailEntity = await _db.Emails.Include(e => e.User)
                    .FirstOrDefaultAsync(e => e.EmailAddress.ToLower() == input);
                user = emailEntity?.User;
            }

            if (user == null || user.IsActive == false)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View();
            }

            var pwd = await _db.Passwords.FirstOrDefaultAsync(p => p.User == user);
            if (pwd == null || !VerifyPassword(password, pwd.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username)
            };

            // Admin role claim
            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = rememberMe });

            return RedirectToAction("Index", "Home");
        }

        // GET /Account/Register
        [HttpGet]
        public IActionResult Register() => View();

        // POST /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var usernameLower = model.Username.Trim().ToLowerInvariant();
            var emailLower = model.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
            {
                ModelState.AddModelError(nameof(model.Username), "Username is already taken.");
                return View(model);
            }

            if (await _db.Emails.AnyAsync(e => e.EmailAddress != null && e.EmailAddress.ToLower() == emailLower))
            {
                ModelState.AddModelError(nameof(model.Email), "Email is already registered.");
                return View(model);
            }

            var user = new User { Username = model.Username.Trim(), IsActive = true };
            var emailEntity = new Email { EmailAddress = model.Email.Trim(), IsVerified = false, User = user };
            var passwordEntity = new Password { PasswordHash = HashPassword(model.Password), User = user };

            _db.Users.Add(user);
            _db.Emails.Add(emailEntity);
            _db.Passwords.Add(passwordEntity);
            await _db.SaveChangesAsync();

            TempData["Message"] = "Registration successful. Please sign in.";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(nameof(Login));
        }

        // POST /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // PBKDF2 password hashing (salted)
        private static string HashPassword(string password)
        {
            const int iterations = 100_000, saltSize = 16, keySize = 32;
            var salt = RandomNumberGenerator.GetBytes(saltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, keySize);
            return $"v1${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string encodedHash)
        {
            if (string.IsNullOrWhiteSpace(encodedHash)) return false;

            var parts = encodedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "v1") return false;

            var iterations = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var salt = Convert.FromBase64String(parts[2]);
            var hash = Convert.FromBase64String(parts[3]);

            var testHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, hash.Length);
            return CryptographicOperations.FixedTimeEquals(testHash, hash);
        }

        // GET /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}