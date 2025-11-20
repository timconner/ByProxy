namespace ByProxy.Infrastructure.Acme {
    public static class AcmeHelpers {
        public static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static JsonSerializerOptions AcmeSerializerOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static HttpContent GenerateAcmeMessage(ECDsa accountKey, string nonce, string url, object? payload) {
            var json = JsonSerializer.Serialize(new AcmeJws(accountKey, nonce, url, payload ?? new object { }), AcmeSerializerOptions);
            return GenerateAcmeMessage(json);
        }

        public static HttpContent GenerateAcmeMessage(string accountUrl, ECDsa accountKey, string nonce, string url, object? payload) {
            var json = JsonSerializer.Serialize(new AcmeJws(accountUrl, accountKey, nonce, url, payload), AcmeSerializerOptions);
            return GenerateAcmeMessage(json);
        }

        private static HttpContent GenerateAcmeMessage(string json) {
            var bytes = Encoding.UTF8.GetBytes(json);
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/jose+json");
            return content;
        }

        public static string GenerateJwkThumbprint(ECDsa accountKey) =>
            Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new AcmeJwk(accountKey), AcmeSerializerOptions))));

        public static (ECDsa PrivateKey, byte[] DerCsr) GenerateCsr(IEnumerable<AcmeIdentifier> identifiers) {
            var hosts = identifiers.Where(_ => _.Type == "dns").Select(_ => _.Value).ToArray();
            if (hosts.Length == 0) throw new ArgumentException("No dns hosts provided.", nameof(identifiers));

            var subject = new X500DistinguishedName($"CN={hosts[0]}");
            var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);

            var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // Server Authentication
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));

            var san = new SubjectAlternativeNameBuilder();
            foreach (var host in hosts) {
                san.AddDnsName(host);
            }
            req.CertificateExtensions.Add(san.Build());

            return (key, req.CreateSigningRequest());
        }

        public static (RSA PrivateKey, byte[] DerCsr) GenerateRsaCsr(IEnumerable<AcmeIdentifier> identifiers) {
            var hosts = identifiers.Where(_ => _.Type == "dns").Select(_ => _.Value).ToArray();
            if (hosts.Length == 0) throw new ArgumentException("No dns hosts provided.", nameof(identifiers));

            var key = RSA.Create(2048);
            var subject = new X500DistinguishedName($"CN={hosts[0]}");
            var req = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));

            var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // Server Authentication
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var san = new SubjectAlternativeNameBuilder();
            foreach (var host in hosts) {
                san.AddDnsName(host);
            }
            req.CertificateExtensions.Add(san.Build());

            return (key, req.CreateSigningRequest());
        }
    }
}
