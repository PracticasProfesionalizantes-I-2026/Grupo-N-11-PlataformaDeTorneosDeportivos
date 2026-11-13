using System;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace BusinessLogic.Security
{
    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32;  // 256 bit
        private const int DefaultIterations = 100_000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        private readonly int _iterations;

        public PasswordHasher(IConfiguration? configuration = null)
        {
            var envIterations = Environment.GetEnvironmentVariable("PASSWORD_HASH_ITERATIONS");
            var configIterations = configuration?["Security:PasswordHashIterations"];

            if (!string.IsNullOrWhiteSpace(envIterations) && int.TryParse(envIterations, out var parsedEnv) && parsedEnv >= 10_000)
            {
                _iterations = parsedEnv;
            }
            else if (!string.IsNullOrWhiteSpace(configIterations) && int.TryParse(configIterations, out var parsedConfig) && parsedConfig >= 10_000)
            {
                _iterations = parsedConfig;
            }
            else
            {
                _iterations = DefaultIterations;
            }
        }

        public string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, Algorithm, KeySize);

            // Formato estándar: {salt}:{hash} codificado en Base64
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            var parts = passwordHash.Split(':');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] hash = Convert.FromBase64String(parts[1]);

            byte[] testHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, Algorithm, KeySize);

            // Comparación de tiempo constante para mitigar timing attacks
            return CryptographicOperations.FixedTimeEquals(hash, testHash);
        }
    }
}
