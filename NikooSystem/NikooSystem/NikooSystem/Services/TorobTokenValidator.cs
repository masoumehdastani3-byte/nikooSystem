using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Web;
using Microsoft.IdentityModel.Tokens;

namespace NikooSystem.Services
{
    /// <summary>اعتبارسنج JWT ترب (EdDSA / Ed25519) — مستند رسمی Torob-Sync</summary>
    public static class TorobTokenValidator
    {
        private const string TorobEd25519X = "t6Mu4T0pBORY11W+QeM35UsmLO3vsf+6yKpFDEImFk0=";

        private static readonly SecurityKey PublicKey = new JsonWebKey(
            "{\"kty\":\"OKP\",\"crv\":\"Ed25519\",\"x\":\"" + TorobEd25519X + "\"}");

        public static string GetRequestAudience(HttpRequestBase request)
        {
            if (request?.Url == null) return "";
            var host = request.Url.Host;
            if (!request.Url.IsDefaultPort)
                host += ":" + request.Url.Port;
            return host;
        }

        public static bool TryValidate(HttpRequestBase request, out string error)
        {
            error = null;
            if (IsSkipValidationEnabled())
                return true;

            var token = request?.Headers["X-Torob-Token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                error = "X-Torob-Token header is missing";
                return false;
            }

            var audience = GetRequestAudience(request);
            try
            {
                var handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    IssuerSigningKey = PublicKey
                }, out _);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsSkipValidationEnabled()
        {
            var v = ConfigurationManager.AppSettings["Torob.SkipJwtValidation"];
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
