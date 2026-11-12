namespace ByProxy.Services {
    public partial class ImportExportService {
        public static class EncryptionParametersV1_25 {
            public const string Version = "1.25";
            public const int Iterations = 600_000;
            public const int SaltSize = 16;
            public const int NonceSize = 12;
            public const int KeySize = 32;
            public const int TagSize = 16;
            public static HashAlgorithmName HashAlgorithm => HashAlgorithmName.SHA256;
        }

        public class EncryptionHeaderV1_25 {
            [JsonInclude]
            [JsonPropertyOrder(1)]
            public string Version = EncryptionParametersV1_25.Version;
            [JsonPropertyOrder(2)]
            public byte[] Salt { get; init; }
            [JsonPropertyOrder(3)]
            public byte[] Nonce { get; init; }
            
            [JsonConstructor]
            public EncryptionHeaderV1_25(byte[] salt, byte[] nonce) {
                if (salt.Length != EncryptionParametersV1_25.SaltSize)
                    throw new ArgumentException($"Salt must be {EncryptionParametersV1_25.SaltSize} bytes.", nameof(salt));
                if (nonce.Length != EncryptionParametersV1_25.NonceSize)
                    throw new ArgumentException($"Nonce must be {EncryptionParametersV1_25.NonceSize} bytes.", nameof(nonce));
                Salt = salt;
                Nonce = nonce;
            }
        }

        private ProxyExportV1_25 DecryptExportV1_25(ReadOnlySpan<byte> headerBytes, ReadOnlySpan<byte> contents, string password) {
            var header = JsonSerializer.Deserialize<EncryptionHeaderV1_25>(headerBytes) ?? throw new InvalidDataException("Invalid encryption header.");
            int tagSize = EncryptionParametersV1_25.TagSize;
            if (contents.Length < tagSize) throw new InvalidDataException("Encrypted payload is truncated.");

            var ciphertext = contents.Slice(0, contents.Length - tagSize);
            var tag = contents.Slice(contents.Length - tagSize, tagSize);

            byte[] plaintext = new byte[ciphertext.Length];
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, header.Salt,
                EncryptionParametersV1_25.Iterations,
                EncryptionParametersV1_25.HashAlgorithm,
                EncryptionParametersV1_25.KeySize);

            using (var aes = new AesGcm(key, tagSize))
                aes.Decrypt(header.Nonce, ciphertext, tag, plaintext, headerBytes);

            return JsonSerializer.Deserialize<ProxyExportV1_25>(plaintext) ?? throw new InvalidDataException("Invalid export contents.");
        }

        public class ProxyExportV1_25 : ProxyExport {
            public SystemSettingsExportV1_25? SystemSettings { get; set; }
            public List<RouteExportV1_25>? Routes { get; set; }
            public List<ClusterExportV1_25>? Clusters { get; set; }
            public List<SniMapExportV1_25>? SniMaps { get; set; }
            public List<AcmeAccountExportV1_25>? AcmeAccounts { get; set; }
            public List<AcmeDnsProviderExportV1_25>? AcmeDnsProviders { get; set; }
            public List<CertificateAuthorityExportV1_25>? CertificateAuthorities { get; set; }
            public List<ServerCertExportV1_25>? ServerCertificates { get; set; }
            public override ProxyExport ToNext() => throw new InvalidOperationException("1.25 is the latest export version.");
        }

        public class SystemSettingsExportV1_25 {
            public bool AdminListenAny { get; set; }
            public int AdminPort { get; set; }
            public Guid? AdminCertId { get; set; }
            public Guid? FallbackCertId { get; set; }
            public long? MaxRequestBodySize { get; set; }
            public required StaticResponseExportV1_25 UnmatchedResponse { get; set; }
            public List<string>? TrustedProxies { get; set; }
            public int? TrustedProxyChainLimit { get; set; }

            public SystemSettingsExport ToCurrent() {
                return new SystemSettingsExport {
                    AdminListenAny = AdminListenAny,
                    AdminPort = AdminPort,
                    AdminCertId = AdminCertId,
                    FallbackCertId = FallbackCertId,
                    MaxRequestBodySize = MaxRequestBodySize,
                    UnmatchedResponse = UnmatchedResponse.ToCurrent(),
                    TrustedProxies = TrustedProxies?.ToList(),
                    TrustedProxyChainLimit = TrustedProxyChainLimit,
                };
            }
        }

        public class RouteExportV1_25 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Description { get; set; }
            public bool Disabled { get; set; }
            public int Order { get; set; }
            public required string ResponseType { get; set; }
            public Guid? ClusterId { get; set; }
            public StaticResponseExportV1_25? StaticResponse { get; set; }
            public List<string>? Methods { get; set; }
            public List<string>? Hosts { get; set; }
            public required List<RouteMatchExportV1_25> MatchCriteria { get; set; }
            public bool PreserveHostHeader { get; set; }
            public bool PreserveClientHeaders { get; set; }
            public List<string>? AllowedRequestHeaders { get; set; }
            public string? XForwardedPrefix { get; set; }
            public string? XForwardedForAction { get; set; }
            public string? XForwardedProtoAction { get; set; }
            public string? XForwardedHostAction { get; set; }
            public string? XForwardedPrefixAction { get; set; }
            public string? Path { get; set; }
            public bool PathIsPrefix { get; set; }
            public int? HttpPort { get; set; }
            public int? HttpsPort { get; set; }
            public bool SuppressHttpsRedirect { get; set; }
            public long? MaxRequestBodySize { get; set; }
            public required List<RequestTransformExportV1_25> RequestTransforms { get; set; }
            public bool PreserveResponseHeaders { get; set; } = true;
            public List<string>? AllowedResponseHeaders { get; set; }
            public List<ResponseTransformExportV1_25> ResponseTransforms { get; set; } = new();
            public string? IpRestrictionMode { get; set; }
            public List<string>? IpRestrictionEntries { get; set; }
            public StaticResponseExportV1_25? IpDenyResponse { get; set; }

            public RouteExport ToCurrent() {
                return new RouteExport {
                    Id = Id,
                    Name = Name,
                    Description = Description,
                    Disabled = Disabled,
                    Order = Order,
                    ResponseType = ResponseType,
                    ClusterId = ClusterId,
                    StaticResponse = StaticResponse?.ToCurrent(),
                    Methods = Methods?.ToList(),
                    Hosts = Hosts?.ToList(),
                    MatchCriteria = MatchCriteria.Select(_ => _.ToCurrent()).ToList(),
                    PreserveHostHeader = PreserveHostHeader,
                    PreserveClientHeaders = PreserveClientHeaders,
                    AllowedRequestHeaders = AllowedRequestHeaders?.ToList(),
                    XForwardedPrefix = XForwardedPrefix,
                    XForwardedForAction = XForwardedForAction,
                    XForwardedProtoAction = XForwardedProtoAction,
                    XForwardedHostAction = XForwardedHostAction,
                    XForwardedPrefixAction = XForwardedPrefixAction,
                    Path = Path,
                    PathIsPrefix = PathIsPrefix,
                    HttpPort = HttpPort,
                    HttpsPort = HttpsPort,
                    SuppressHttpsRedirect = SuppressHttpsRedirect,
                    MaxRequestBodySize = MaxRequestBodySize,
                    RequestTransforms = RequestTransforms.Select(_ => _.ToCurrent()).ToList(),
                    PreserveResponseHeaders = PreserveResponseHeaders,
                    AllowedResponseHeaders = AllowedResponseHeaders?.ToList(),
                    ResponseTransforms = ResponseTransforms.Select(_ => _.ToCurrent()).ToList(),
                    IpRestrictionMode = IpRestrictionMode,
                    IpRestrictionEntries = IpRestrictionEntries?.ToList(),
                    IpDenyResponse = IpDenyResponse?.ToCurrent(),
                };
            }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(RequestPathTransformExportV1_25), "Path")]
        [JsonDerivedType(typeof(RequestMethodTransformExportV1_25), "Method")]
        [JsonDerivedType(typeof(RequestHeaderTransformExportV1_25), "Header")]
        [JsonDerivedType(typeof(RequestQueryTransformExportV1_25), "Query")]
        public abstract class RequestTransformExportV1_25 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
            public abstract RequestTransformExport ToCurrent();
        }

        public class RequestPathTransformExportV1_25 : RequestTransformExportV1_25 {
            public required string Mode { get; set; }
            public required string PathString { get; set; }

            public override RequestTransformExport ToCurrent() => new RequestPathTransformExport {
                Id = Id,
                RouteId = RouteId,
                Mode = Mode,
                PathString = PathString
            };
        }

        public class RequestMethodTransformExportV1_25 : RequestTransformExportV1_25 {
            public required string FromHttpMethod { get; set; }
            public required string ToHttpMethod { get; set; }

            public override RequestTransformExport ToCurrent() => new RequestMethodTransformExport {
                Id = Id,
                RouteId = RouteId,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }

        public class RequestHeaderTransformExportV1_25 : RequestTransformExportV1_25 {
            public required string Operation { get; set; }
            public required string HeaderName { get; set; }
            public string? HeaderValue { get; set; }

            public override RequestTransformExport ToCurrent() => new RequestHeaderTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = Operation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }

        public class RequestQueryTransformExportV1_25 : RequestTransformExportV1_25 {
            public required string Operation { get; set; }
            public required string QueryKey { get; set; }
            public string? QueryValue { get; set; }

            public override RequestTransformExport ToCurrent() => new RequestQueryTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = Operation,
                QueryKey = QueryKey,
                QueryValue = QueryValue
            };
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(ResponseHeaderTransformExportV1_25), "Header")]
        public abstract class ResponseTransformExportV1_25 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }

            public abstract ResponseTransformExport ToCurrent();
        }

        public class ResponseHeaderTransformExportV1_25 : ResponseTransformExportV1_25 {
            public required string Operation { get; set; }
            public required string HeaderName { get; set; }
            public string? HeaderValue { get; set; }

            public override ResponseTransformExport ToCurrent() => new ResponseHeaderTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = Operation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(RouteHeaderMatchExportV1_25), "Header")]
        [JsonDerivedType(typeof(RouteQueryMatchExportV1_25), "Query")]
        public abstract class RouteMatchExportV1_25 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
            public required string Key { get; set; }
            public List<string>? Values { get; set; }
            public bool? CaseSensitiveValues { get; set; }
            public abstract RouteMatchExport ToCurrent();
        }
        public class RouteHeaderMatchExportV1_25 : RouteMatchExportV1_25 {
            public required string Mode { get; set; }
            public override RouteMatchExport ToCurrent() => new RouteHeaderMatchExport {
                Id = Id,
                RouteId = RouteId,
                Key = Key,
                Values = Values?.ToList(),
                CaseSensitiveValues = CaseSensitiveValues,
                Mode = Mode
            };
        }
        public class RouteQueryMatchExportV1_25 : RouteMatchExportV1_25 {
            public required string Mode { get; set; }
            public override RouteMatchExport ToCurrent() => new RouteQueryMatchExport {
                Id = Id,
                RouteId = RouteId,
                Key = Key,
                Values = Values?.ToList(),
                CaseSensitiveValues = CaseSensitiveValues,
                Mode = Mode
            };
        }

        public class StaticResponseExportV1_25 {
            public int HttpStatusCode { get; set; }
            public bool UseTarpit { get; set; }
            public string? Body { get; set; }
            public Dictionary<string, string>? Headers { get; set; }

            public StaticResponseExport ToCurrent() {
                return new StaticResponseExport {
                    HttpStatusCode = HttpStatusCode,
                    UseTarpit = UseTarpit,
                    Body = Body,
                    Headers = Headers == null ? null : Headers.ToDictionary()
                };
            }
        }

        public class ClusterExportV1_25 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Description { get; set; }
            public required List<DestinationExportV1_25> Destinations { get; set; }
            public string? LoadBalancing { get; set; }
            public bool StickySessions { get; set; }
            public bool AcceptAnyCert { get; set; }

            public ClusterExport ToCurrent() {
                return new ClusterExport {
                    Id = Id,
                    Name = Name,
                    Description = Description,
                    Destinations = Destinations.Select(_ => _.ToCurrent()).ToList(),
                    LoadBalancing = LoadBalancing,
                    StickySessions = StickySessions,
                    AcceptAnyCert = AcceptAnyCert
                };
            }
        }

        public class DestinationExportV1_25 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public Guid ClusterId { get; set; }
            public int Order { get; set; }
            public required string Address { get; set; }
            public string? Health { get; set; }

            public DestinationExport ToCurrent() {
                return new DestinationExport {
                    Id = Id,
                    Name = Name,
                    ClusterId = ClusterId,
                    Order = Order,
                    Address = Address,
                    Health = Health
                };
            }
        }

        public class SniMapExportV1_25 {
            public required string Host { get; set; }
            public Guid CertificateId { get; set; }

            public SniMapExport ToCurrent() {
                return new SniMapExport {
                    Host = Host,
                    CertificateId = CertificateId
                };
            }
        }

        public class AcmeAccountExportV1_25 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Provider { get; set; }
            public required string Url { get; set; }
            public bool Hidden { get; set; }
            public required string Key { get; set; }

            public AcmeAccountExport ToCurrent() {
                return new AcmeAccountExport {
                    Id = Id,
                    Name = Name,
                    Provider = Provider,
                    Url = Url,
                    Hidden = Hidden,
                    Key = Key
                };
            }
        }

        public class AcmeDnsProviderExportV1_25 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public string? Script { get; set; }

            public AcmeDnsProviderExport ToCurrent() {
                return new AcmeDnsProviderExport {
                    Id = Id,
                    Name = Name,
                    Script = Script
                };
            }
        }

        public class CertificateAuthorityExportV1_25 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public bool Hidden { get; set; }
            public required string Material { get; set; }

            public CertificateAuthorityExport ToCurrent() {
                return new CertificateAuthorityExport {
                    Id = Id,
                    Name = Name,
                    Hidden = Hidden,
                    Material = Material
                };
            }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(IssuedCertExportV1_25), "Issued")]
        [JsonDerivedType(typeof(AcmeCertExportV1_25), "ACME")]
        [JsonDerivedType(typeof(ImportedCertExportV1_25), "Imported")]
        public abstract class ServerCertExportV1_25 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public bool Hidden { get; set; }
            public required string Material { get; set; }
            public abstract ServerCertExport ToCurrent();
        }

        public class IssuedCertExportV1_25 : ServerCertExportV1_25 {
            public Guid IssuingCAId { get; set; }
            public override ServerCertExport ToCurrent() {
                return new IssuedCertExport {
                    Id = Id,
                    Name = Name,
                    Hidden = Hidden,
                    IssuingCAId = IssuingCAId,
                    Material = Material
                };
            }
        }

        public class AcmeCertExportV1_25 : ServerCertExportV1_25 {
            public Guid AcmeAccountId { get; set; }
            public DateTime? LastAttempt { get; set; }
            public required List<AcmeCertHostExportV1_25> Hosts { get; set; }
            public override ServerCertExport ToCurrent() {
                return new AcmeCertExport {
                    Id = Id,
                    Name = Name,
                    Hidden = Hidden,
                    AcmeAccountId = AcmeAccountId,
                    LastAttempt = LastAttempt,
                    Hosts = Hosts.Select(_ => _.ToCurrent()).ToList(),
                    Material = Material
                };
            }
        }

        public class ImportedCertExportV1_25 : ServerCertExportV1_25 {
            public override ServerCertExport ToCurrent() {
                return new ImportedCertExport {
                    Id = Id,
                    Name = Name,
                    Hidden = Hidden,
                    Material = Material
                };
            }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(AcmeHttpCertHostExportV1_25), AcmeChallengeType.HTTP_01)]
        [JsonDerivedType(typeof(AcmeDnsCertHostExportV1_25), AcmeChallengeType.DNS_01)]
        public abstract class AcmeCertHostExportV1_25 {
            public required string Host { get; set; }
            public abstract AcmeCertHostExport ToCurrent();
        }

        public class AcmeHttpCertHostExportV1_25 : AcmeCertHostExportV1_25 {
            public override AcmeCertHostExport ToCurrent() {
                return new AcmeHttpCertHostExport {
                    Host = Host
                };
            }
        }

        public class AcmeDnsCertHostExportV1_25 : AcmeCertHostExportV1_25 {
            public Guid DnsProviderId { get; set; }

            public override AcmeCertHostExport ToCurrent() {
                return new AcmeDnsCertHostExport {
                    Host = Host,
                    DnsProviderId = DnsProviderId
                };
            }
        }
    }
}
