namespace ByProxy.Infrastructure.Acme {
    public class AcmeJws {
        public string Protected { get; init; }
        public string Payload { get; init; }
        public string Signature { get; init; }

        public AcmeJws(ECDsa accountKey, string nonce, string url, object payload) : this(
            header: AcmeHelpers.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new AcmeProtectedJwkHeader(accountKey, nonce, url), AcmeHelpers.AcmeSerializerOptions)),
            accountKey: accountKey,
            payload: payload
        ) { }

        public AcmeJws(string kid, ECDsa accountKey, string nonce, string url, object? payload) : this(
            header: AcmeHelpers.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new AcmeProtectedKidHeader(kid, nonce, url), AcmeHelpers.AcmeSerializerOptions)),
            accountKey: accountKey,
            payload: payload
        ) { }

        private AcmeJws(string header, ECDsa accountKey, object? payload) {
            Protected = header;
            Payload = payload == null ? string.Empty : AcmeHelpers.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, AcmeHelpers.AcmeSerializerOptions));

            var signature = accountKey.SignData(Encoding.ASCII.GetBytes($"{Protected}.{Payload}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            Signature = AcmeHelpers.Base64UrlEncode(signature);
        }
    }
    
    public class AcmeProtectedKidHeader {
        public string Alg => "ES256";
        public string Kid { get; init; }
        public string Nonce { get; init; }
        public string Url { get; init; }

        public AcmeProtectedKidHeader(string kid, string nonce, string url) {
            Kid = kid;
            Nonce = nonce;
            Url = url;
        }
    }

    public class AcmeProtectedJwkHeader {
        public string Alg => "ES256";
        public AcmeJwk Jwk { get; init; }
        public string Nonce { get; init; }
        public string Url { get; init; }

        public AcmeProtectedJwkHeader(ECDsa accountKey, string nonce, string url) {
            Jwk = new AcmeJwk(accountKey);
            Nonce = nonce;
            Url = url;
        }
    }

    public class AcmeJwk {
        [JsonPropertyOrder(0)]
        public string Crv => "P-256";

        [JsonPropertyOrder(1)]
        public string Kty => "EC";

        [JsonPropertyOrder(2)]
        public string X { get; init; }
        
        [JsonPropertyOrder(3)]
        public string Y { get; init; }

        public AcmeJwk(ECDsa accountKey) {
            var parameters = accountKey.ExportParameters(false);
            if (parameters.Q.X == null || parameters.Q.Y == null) throw new Exception("Failed to export parameters from account key.");

            X = AcmeHelpers.Base64UrlEncode(parameters.Q.X);
            Y = AcmeHelpers.Base64UrlEncode(parameters.Q.Y);
        }
    }
}
