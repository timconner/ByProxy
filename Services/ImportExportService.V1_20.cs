namespace ByProxy.Services {
    public partial class ImportExportService {
        public static class EncryptionParametersV1_20 {
            public const string Version = "1.20";
            public const int Iterations = 600_000;
            public const int SaltSize = 16;
            public const int NonceSize = 12;
            public const int KeySize = 32;
            public const int TagSize = 16;
            public static HashAlgorithmName HashAlgorithm => HashAlgorithmName.SHA256;
        }

        public class EncryptionHeaderV1_20 {
            [JsonInclude]
            [JsonPropertyOrder(1)]
            public string Version = EncryptionParametersV1_20.Version;
            [JsonPropertyOrder(2)]
            public byte[] Salt { get; init; }
            [JsonPropertyOrder(3)]
            public byte[] Nonce { get; init; }
            
            [JsonConstructor]
            public EncryptionHeaderV1_20(byte[] salt, byte[] nonce) {
                if (salt.Length != EncryptionParametersV1_20.SaltSize)
                    throw new ArgumentException($"Salt must be {EncryptionParametersV1_20.SaltSize} bytes.", nameof(salt));
                if (nonce.Length != EncryptionParametersV1_20.NonceSize)
                    throw new ArgumentException($"Nonce must be {EncryptionParametersV1_20.NonceSize} bytes.", nameof(nonce));
                Salt = salt;
                Nonce = nonce;
            }
        }

        private ProxyExportV1_20 DecryptExportV1_20(ReadOnlySpan<byte> headerBytes, ReadOnlySpan<byte> contents, string password) {
            var header = JsonSerializer.Deserialize<EncryptionHeaderV1_20>(headerBytes) ?? throw new InvalidDataException("Invalid encryption header.");
            int tagSize = EncryptionParametersV1_20.TagSize;
            if (contents.Length < tagSize) throw new InvalidDataException("Encrypted payload is truncated.");

            var ciphertext = contents.Slice(0, contents.Length - tagSize);
            var tag = contents.Slice(contents.Length - tagSize, tagSize);

            byte[] plaintext = new byte[ciphertext.Length];
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, header.Salt,
                EncryptionParametersV1_20.Iterations,
                EncryptionParametersV1_20.HashAlgorithm,
                EncryptionParametersV1_20.KeySize);

            using (var aes = new AesGcm(key, tagSize))
                aes.Decrypt(header.Nonce, ciphertext, tag, plaintext, headerBytes);

            return JsonSerializer.Deserialize<ProxyExportV1_20>(plaintext) ?? throw new InvalidDataException("Invalid export contents.");
        }

        public class ProxyExportV1_20 : ProxyExport {
            public SystemSettingsExportV1_20? SystemSettings { get; set; }
            public List<RouteExportV1_20>? Routes { get; set; }
            public List<ClusterExportV1_20>? Clusters { get; set; }
            public List<SniMapExportV1_20>? SniMaps { get; set; }
            public List<AcmeAccountExportV1_20>? AcmeAccounts { get; set; }
            public List<AcmeDnsProviderExportV1_20>? AcmeDnsProviders { get; set; }
            public List<CertificateAuthorityExportV1_20>? CertificateAuthorities { get; set; }
            public List<ServerCertExportV1_20>? ServerCertificates { get; set; }
            public override ProxyExport ToNext() => throw new InvalidOperationException("1.20 is the latest export version.");
        }

        public class SystemSettingsExportV1_20 {
            public bool AdminListenAny { get; set; }
            public int AdminPort { get; set; }
            public Guid? AdminCertId { get; set; }
            public Guid? FallbackCertId { get; set; }
            public long? MaxRequestBodySize { get; set; }
            public int UnmatchedStatus { get; set; }
            public bool Tarpit { get; set; }

            public SystemSettingsExport ToCurrent() {
                return new SystemSettingsExport {
                    AdminListenAny = AdminListenAny,
                    AdminPort = AdminPort,
                    AdminCertId = AdminCertId,
                    FallbackCertId = FallbackCertId,
                    MaxRequestBodySize = MaxRequestBodySize,
                    UnmatchedStatus = UnmatchedStatus,
                    Tarpit = Tarpit,
                };
            }
        }

        public class RouteExportV1_20 {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public bool Disabled { get; set; }
            public int Order { get; set; }
            public string ResponseType { get; set; }
            public Guid? ClusterId { get; set; }
            public int? HttpStatusCode { get; set; }
            public string? RedirectLocation { get; set; }
            public List<string>? Methods { get; set; }
            public List<string>? Hosts { get; set; }
            public List<RouteMatchExportV1_20> MatchCriteria { get; set; }
            public bool PreserveHostHeader { get; set; }
            public bool PreserveClientHeaders { get; set; }
            public List<string>? AllowedHeaders { get; set; }
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
            public List<RouteTransformExportV1_20> Transforms { get; set; }

            public RouteExport ToCurrent() {
                return new RouteExport {
                    Id = Id,
                    Name = Name,
                    Description = Description,
                    Disabled = Disabled,
                    Order = Order,
                    ResponseType = ResponseType,
                    ClusterId = ClusterId,
                    RedirectLocation = RedirectLocation,
                    HttpStatusCode = HttpStatusCode,
                    Methods = Methods?.ToList(),
                    Hosts = Hosts?.ToList(),
                    MatchCriteria = MatchCriteria.Select(_ => _.ToCurrent()).ToList(),
                    PreserveHostHeader = PreserveHostHeader,
                    PreserveClientHeaders = PreserveClientHeaders,
                    AllowedHeaders = AllowedHeaders?.ToList(),
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
                    Transforms = Transforms.Select(_ => _.ToCurrent()).ToList()
                };
            }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(RoutePathTransformExportV1_20), "Path")]
        [JsonDerivedType(typeof(RouteMethodTransformExportV1_20), "Method")]
        [JsonDerivedType(typeof(RouteHeaderTransformExportV1_20), "Header")]
        [JsonDerivedType(typeof(RouteQueryTransformExportV1_20), "Query")]
        public abstract class RouteTransformExportV1_20 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
            public abstract RouteTransformExport ToCurrent();
        }

        public class RoutePathTransformExportV1_20 : RouteTransformExportV1_20 {
            public string Mode { get; set; }
            public string PathString { get; set; }

            public override RouteTransformExport ToCurrent() => new RoutePathTransformExport {
                Id = Id,
                RouteId = RouteId,
                Mode = Mode,
                PathString = PathString
            };
        }

        public class RouteMethodTransformExportV1_20 : RouteTransformExportV1_20 {
            public string FromHttpMethod { get; set; }
            public string ToHttpMethod { get; set; }

            public override RouteTransformExport ToCurrent() => new RouteMethodTransformExport {
                Id = Id,
                RouteId = RouteId,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }

        public class RouteHeaderTransformExportV1_20 : RouteTransformExportV1_20 {
            public string Operation { get; set; }
            public string HeaderName { get; set; }
            public string? HeaderValue { get; set; }

            public override RouteTransformExport ToCurrent() => new RouteHeaderTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = Operation,
                HeaderName = HeaderName,
                HeaderValue = HeaderValue
            };
        }

        public class RouteQueryTransformExportV1_20 : RouteTransformExportV1_20 {
            public string Operation { get; set; }
            public string QueryKey { get; set; }
            public string? QueryValue { get; set; }

            public override RouteTransformExport ToCurrent() => new RouteQueryTransformExport {
                Id = Id,
                RouteId = RouteId,
                Operation = Operation,
                QueryKey = QueryKey,
                QueryValue = QueryValue
            };
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(RouteHeaderMatchExportV1_20), "Header")]
        [JsonDerivedType(typeof(RouteQueryMatchExportV1_20), "Query")]
        public abstract class RouteMatchExportV1_20 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
            public string Key { get; set; }
            public List<string>? Values { get; set; }
            public bool? CaseSensitiveValues { get; set; }
            public abstract RouteMatchExport ToCurrent();
        }
        public class RouteHeaderMatchExportV1_20 : RouteMatchExportV1_20 {
            public string Mode { get; set; }
            public override RouteMatchExport ToCurrent() => new RouteHeaderMatchExport {
                Id = Id,
                RouteId = RouteId,
                Key = Key,
                Values = Values?.ToList(),
                CaseSensitiveValues = CaseSensitiveValues,
                Mode = Mode
            };
        }
        public class RouteQueryMatchExportV1_20 : RouteMatchExportV1_20 {
            public string Mode { get; set; }
            public override RouteMatchExport ToCurrent() => new RouteQueryMatchExport {
                Id = Id,
                RouteId = RouteId,
                Key = Key,
                Values = Values?.ToList(),
                CaseSensitiveValues = CaseSensitiveValues,
                Mode = Mode
            };
        }

        public class ClusterExportV1_20 {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public List<DestinationExportV1_20> Destinations { get; set; }
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

        public class DestinationExportV1_20 {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public Guid ClusterId { get; set; }
            public int Order { get; set; }
            public string Address { get; set; }
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

        public class SniMapExportV1_20 {
            public string Host { get; set; }
            public Guid CertificateId { get; set; }

            public SniMapExport ToCurrent() {
                return new SniMapExport {
                    Host = Host,
                    CertificateId = CertificateId
                };
            }
        }

        public class AcmeAccountExportV1_20 {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Provider { get; set; }
            public string Url { get; set; }
            public bool Hidden { get; set; }
            public string Key { get; set; }

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

        public class AcmeDnsProviderExportV1_20 {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string? Script { get; set; }

            public AcmeDnsProviderExport ToCurrent() {
                return new AcmeDnsProviderExport {
                    Id = Id,
                    Name = Name,
                    Script = Script
                };
            }
        }

        public class CertificateAuthorityExportV1_20 {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public bool Hidden { get; set; }
            public string Material { get; set; }

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
        [JsonDerivedType(typeof(IssuedCertExportV1_20), "Issued")]
        [JsonDerivedType(typeof(AcmeCertExportV1_20), "ACME")]
        [JsonDerivedType(typeof(ImportedCertExportV1_20), "Imported")]
        public abstract class ServerCertExportV1_20 {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public bool Hidden { get; set; }
            public string Material { get; set; }
            public abstract ServerCertExport ToCurrent();
        }

        public class IssuedCertExportV1_20 : ServerCertExportV1_20 {
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

        public class AcmeCertExportV1_20 : ServerCertExportV1_20 {
            public Guid AcmeAccountId { get; set; }
            public DateTime? LastAttempt { get; set; }
            public List<AcmeCertHostExportV1_20> Hosts { get; set; }
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

        public class ImportedCertExportV1_20 : ServerCertExportV1_20 {
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
        [JsonDerivedType(typeof(AcmeHttpCertHostExportV1_20), AcmeChallengeType.HTTP_01)]
        [JsonDerivedType(typeof(AcmeDnsCertHostExportV1_20), AcmeChallengeType.DNS_01)]
        public abstract class AcmeCertHostExportV1_20 {
            public string Host { get; set; }
            public abstract AcmeCertHostExport ToCurrent();
        }

        public class AcmeHttpCertHostExportV1_20 : AcmeCertHostExportV1_20 {
            public override AcmeCertHostExport ToCurrent() {
                return new AcmeHttpCertHostExport {
                    Host = Host
                };
            }
        }

        public class AcmeDnsCertHostExportV1_20 : AcmeCertHostExportV1_20 {
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
