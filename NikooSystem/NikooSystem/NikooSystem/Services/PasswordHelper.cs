using System;
using System.Security.Cryptography;
using System.Text;

namespace NikooSystem.Services
{
    public static class PasswordHelper
    {
        private const string PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static string Hash(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool Verify(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            return string.Equals(Hash(password), hash, StringComparison.OrdinalIgnoreCase);
        }

        public static string GenerateTemporaryPassword(int length = 10)
        {
            if (length < 6) length = 6;
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var chars = new char[length];
            for (var i = 0; i < length; i++) chars[i] = PasswordChars[bytes[i] % PasswordChars.Length];
            return new string(chars);
        }
    }
}
