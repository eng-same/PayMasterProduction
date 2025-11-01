using Microsoft.Extensions.Caching.Memory;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.DAL.Models;

namespace Application.BLL.Servicies
{
    public class LiveQrService
    {
        private readonly byte[] _hmacKey; // still private
        private readonly IMemoryCache _cache;

        public LiveQrService(byte[] hmacKey, IMemoryCache cache)
        {
            _hmacKey = hmacKey ?? throw new ArgumentNullException(nameof(hmacKey));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // Create compact QR string (v1.payload.sig)
        public string CreateQrString(CompanyQRCode record)
        {
            var payloadObj = new
            {
                id = record.Id,
                token = record.QRCodeToken,
                exp = record.ExpiryDate.ToUniversalTime().ToString("o"),
                live = DateTime.UtcNow.ToString("o")
            };

            var payloadJson = JsonSerializer.Serialize(payloadObj);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var payloadB64 = Base64Url.Encode(payloadBytes);

            byte[] sig;
            using (var hmac = new HMACSHA256(_hmacKey))
            {
                sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
            }
            var sigB64 = Base64Url.Encode(sig);
            return $"v1.{payloadB64}.{sigB64}";
        }

        public byte[] CreateQrPng(CompanyQRCode record, string baseUrl = null, int pixelsPerModule = 6)
        {
            // Build the raw payload (JSON in this example)
            var payload = CreateQrString(record);

            // If caller provided a baseUrl, wrap the payload into a URL query parameter (URL-encode it)
            string qrString;
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                // sanitize baseUrl (no trailing slash) and choose a query param name (e.g. data)
                var cleanBase = baseUrl.TrimEnd('/');
                var encoded = Uri.EscapeDataString(payload);
                qrString = $"{cleanBase}/?data={encoded}";

                // Alternatively, if you prefer a path-based payload:
                // qrString = $"{cleanBase}/p/{encoded}";
            }
            else
            {
                // if no baseUrl passed, QR will contain the payload itself (JSON string)
                qrString = payload;
            }

            using (var generator = new QRCodeGenerator())
            {
                var data = generator.CreateQrCode(qrString, QRCodeGenerator.ECCLevel.M);
                using (var qrCode = new PngByteQRCode(data))
                {
                    return qrCode.GetGraphic(pixelsPerModule);
                }
            }
        }

        // NEW: Verify signature (uses private key internally)
        public bool VerifySignature(string payloadB64, byte[] sigBytes)
        {
            if (string.IsNullOrEmpty(payloadB64)) return false;
            if (sigBytes == null || sigBytes.Length == 0) return false;

            using (var hmac = new HMACSHA256(_hmacKey))
            {
                var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
                return CryptographicOperations.FixedTimeEquals(expected, sigBytes);
            }
        }

        // Public replay-protection helpers
        public void MarkLiveUsed(string token, string liveIso, TimeSpan ttl)
        {
            var key = $"used:{token}:{liveIso}";
            _cache.Set(key, true, ttl);
        }

        public bool IsLiveUsed(string token, string liveIso)
        {
            var key = $"used:{token}:{liveIso}";
            return _cache.TryGetValue(key, out _);
        }
    }
}
