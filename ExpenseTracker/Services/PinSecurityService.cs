using ExpenseTracker.Models;
using System.Security.Cryptography;
using System.Text;

namespace ExpenseTracker.Services
{
    public class PinSecurityService
    {
        public const string AuthCookieName = "ExpenseTracker_PinAuth";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public bool IsValidPinFormat(string? pin)
        {
            if (string.IsNullOrWhiteSpace(pin))
            {
                return false;
            }

            return pin.Length >= 4 && pin.Length <= 6 && pin.All(char.IsDigit);
        }

        public string HashPin(string pin)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(pin),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public bool VerifyPin(string pin, string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(pin) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            string[] parts = storedHash.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);
                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(pin),
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public string GenerateSessionToken(User user)
        {
            string rawPayload = $"{user.Id}:{user.PinHash}";
            byte[] tokenBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload));
            return Convert.ToHexString(tokenBytes);
        }

        public void SetAuthCookie(HttpContext context, User user)
        {
            string token = GenerateSessionToken(user);
            CookieOptions options = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            };

            context.Response.Cookies.Append(AuthCookieName, token, options);
        }

        public void ClearAuthCookie(HttpContext context)
        {
            context.Response.Cookies.Delete(AuthCookieName);
        }
    }
}