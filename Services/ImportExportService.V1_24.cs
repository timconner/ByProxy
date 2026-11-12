namespace ByProxy.Services {
    public partial class ImportExportService {
        public static class EncryptionParametersV1_24 {
            public const string Version = "1.24";
            public const int Iterations = 600_000;
            public const int SaltSize = 16;
            public const int NonceSize = 12;
            public const int KeySize = 32;
            public const int TagSize = 16;
            public static HashAlgorithmName HashAlgorithm => HashAlgorithmName.SHA256;
        }

        public class EncryptionHeaderV1_24 {
            [JsonInclude]
            [JsonPropertyOrder(1)]
            public string Version = EncryptionParametersV1_24.Version;
            [JsonPropertyOrder(2)]
            public byte[] Salt { get; init; }
            [JsonPropertyOrder(3)]
            public byte[] Nonce { get; init; }
            
            [JsonConstructor]
            public EncryptionHeaderV1_24(byte[] salt, byte[] nonce) {
                if (salt.Length != EncryptionParametersV1_24.SaltSize)
                    throw new ArgumentException($"Salt must be {EncryptionParametersV1_24.SaltSize} bytes.", nameof(salt));
                if (nonce.Length != EncryptionParametersV1_24.NonceSize)
                    throw new ArgumentException($"Nonce must be {EncryptionParametersV1_24.NonceSize} bytes.", nameof(nonce));
                Salt = salt;
                Nonce = nonce;
            }
        }

        private ProxyExportV1_24 DecryptExportV1_24(ReadOnlySpan<byte> headerBytes, ReadOnlySpan<byte> contents, string password) {
            var header = JsonSerializer.Deserialize<EncryptionHeaderV1_24>(headerBytes) ?? throw new InvalidDataException("Invalid encryption header.");
            int tagSize = EncryptionParametersV1_24.TagSize;
            if (contents.Length < tagSize) throw new InvalidDataException("Encrypted payload is truncated.");

            var ciphertext = contents.Slice(0, contents.Length - tagSize);
            var tag = contents.Slice(contents.Length - tagSize, tagSize);

            byte[] plaintext = new byte[ciphertext.Length];
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, header.Salt,
                EncryptionParametersV1_24.Iterations,
                EncryptionParametersV1_24.HashAlgorithm,
                EncryptionParametersV1_24.KeySize);

            using (var aes = new AesGcm(key, tagSize))
                aes.Decrypt(header.Nonce, ciphertext, tag, plaintext, headerBytes);

            return JsonSerializer.Deserialize<ProxyExportV1_24>(plaintext) ?? throw new InvalidDataException("Invalid export contents.");
        }

        public class ProxyExportV1_24 : ProxyExport {
            public SystemSettingsExportV1_24? SystemSettings { get; set; }
            public List<RouteExportV1_24>? Routes { get; set; }
            public List<ClusterExportV1_24>? Clusters { get; set; }
            public List<SniMapExportV1_24>? SniMaps { get; set; }
            public List<AcmeAccountExportV1_24>? AcmeAccounts { get; set; }
            public List<AcmeDnsProviderExportV1_24>? AcmeDnsProviders { get; set; }
            public List<CertificateAuthorityExportV1_24>? CertificateAuthorities { get; set; }
            public List<ServerCertExportV1_24>? ServerCertificates { get; set; }
            public override ProxyExport ToNext() {
                return new ProxyExportV1_25 {
                    SystemSettings = SystemSettings == null ? null : new SystemSettingsExportV1_25 {
                        AdminListenAny = SystemSettings.AdminListenAny,
                        AdminPort = SystemSettings.AdminPort,
                        AdminCertId = SystemSettings.AdminCertId,
                        FallbackCertId = SystemSettings.FallbackCertId,
                        MaxRequestBodySize = SystemSettings.MaxRequestBodySize,
                        UnmatchedResponse = new StaticResponseExportV1_25 {
                            HttpStatusCode = SystemSettings.UnmatchedStatus,
                            UseTarpit = SystemSettings.Tarpit
                        },
                        TrustedProxies = null,
                        TrustedProxyChainLimit = null
                    },

                    Routes = Routes?.Select(route => {
                        StaticResponseExportV1_25? staticResponse;

                        switch (route.ResponseType) {
                            case "Cluster":
                                staticResponse = null;
                                break;
                            case "Redirect":
                                var redirectHeaders = route.StaticResponseHeaders?
                                    .Where(header => !string.Equals(header.Key, "Location", StringComparison.OrdinalIgnoreCase))
                                    .ToDictionary(header => header.Key, header => header.Value)
                                    ?? new Dictionary<string, string>();

                                redirectHeaders["Location"] = route.RedirectLocation ?? string.Empty;

                                staticResponse = new StaticResponseExportV1_25 {
                                    HttpStatusCode = route.HttpStatusCode ?? 307,
                                    UseTarpit = false,
                                    Body = route.StaticResponseBody,
                                    Headers = redirectHeaders.OrderBy(header => header.Key, StringComparer.Ordinal).ToDictionary(header => header.Key, header => header.Value)
                                };
                                break;
                            case "Static":
                            case "Tarpit":
                                staticResponse = new StaticResponseExportV1_25 {
                                    HttpStatusCode = route.HttpStatusCode ?? (route.ResponseType == "Tarpit" ? 500 : 200),
                                    UseTarpit = route.ResponseType == "Tarpit",
                                    Body = route.StaticResponseBody,
                                    Headers = route.StaticResponseHeaders?.OrderBy(header => header.Key, StringComparer.Ordinal).ToDictionary(header => header.Key, header => header.Value)
                                };
                                break;
                            default:
                                throw new NotSupportedException($"No response upgrade mapping for {route.ResponseType}");
                        }

                        return new RouteExportV1_25 {
                            Id = route.Id,
                            Name = route.Name,
                            Description = route.Description,
                            Disabled = route.Disabled,
                            Order = route.Order,
                            ResponseType = route.ResponseType,
                            ClusterId = route.ClusterId,
                            StaticResponse = staticResponse,
                            Methods = route.Methods?.ToList(),
                            Hosts = route.Hosts?.ToList(),

                            MatchCriteria = route.MatchCriteria.Select(match => match switch {
                                RouteHeaderMatchExportV1_24 header => (RouteMatchExportV1_25)new RouteHeaderMatchExportV1_25 {
                                    Id = header.Id,
                                    RouteId = header.RouteId,
                                    Key = header.Key,
                                    Values = header.Values?.ToList(),
                                    CaseSensitiveValues = header.CaseSensitiveValues,
                                    Mode = header.Mode
                                },
                                RouteQueryMatchExportV1_24 query => new RouteQueryMatchExportV1_25 {
                                    Id = query.Id,
                                    RouteId = query.RouteId,
                                    Key = query.Key,
                                    Values = query.Values?.ToList(),
                                    CaseSensitiveValues = query.CaseSensitiveValues,
                                    Mode = query.Mode
                                },
                                _ => throw new NotSupportedException($"No upgrade mapping for {match.GetType().Name}")
                            }).ToList(),

                            PreserveHostHeader = route.PreserveHostHeader,
                            PreserveClientHeaders = route.PreserveClientHeaders,
                            AllowedRequestHeaders = route.AllowedRequestHeaders?.ToList(),
                            XForwardedPrefix = route.XForwardedPrefix,
                            XForwardedForAction = route.XForwardedForAction,
                            XForwardedProtoAction = route.XForwardedProtoAction,
                            XForwardedHostAction = route.XForwardedHostAction,
                            XForwardedPrefixAction = route.XForwardedPrefixAction,
                            Path = route.Path,
                            PathIsPrefix = route.PathIsPrefix,
                            HttpPort = route.HttpPort,
                            HttpsPort = route.HttpsPort,
                            SuppressHttpsRedirect = route.SuppressHttpsRedirect,
                            MaxRequestBodySize = route.MaxRequestBodySize,

                            RequestTransforms = route.RequestTransforms.Select(transform => transform switch {
                                RequestPathTransformExportV1_24 path => (RequestTransformExportV1_25)new RequestPathTransformExportV1_25 {
                                    Id = path.Id,
                                    RouteId = path.RouteId,
                                    Mode = path.Mode,
                                    PathString = path.PathString
                                },
                                RequestMethodTransformExportV1_24 method => new RequestMethodTransformExportV1_25 {
                                    Id = method.Id,
                                    RouteId = method.RouteId,
                                    FromHttpMethod = method.FromHttpMethod,
                                    ToHttpMethod = method.ToHttpMethod
                                },
                                RequestHeaderTransformExportV1_24 header => new RequestHeaderTransformExportV1_25 {
                                    Id = header.Id,
                                    RouteId = header.RouteId,
                                    Operation = header.Operation,
                                    HeaderName = header.HeaderName,
                                    HeaderValue = header.HeaderValue
                                },
                                RequestQueryTransformExportV1_24 query => new RequestQueryTransformExportV1_25 {
                                    Id = query.Id,
                                    RouteId = query.RouteId,
                                    Operation = query.Operation,
                                    QueryKey = query.QueryKey,
                                    QueryValue = query.QueryValue
                                },
                                _ => throw new NotSupportedException($"No upgrade mapping for {transform.GetType().Name}")
                            }).ToList(),

                            PreserveResponseHeaders = route.PreserveResponseHeaders,
                            AllowedResponseHeaders = route.AllowedResponseHeaders?.ToList(),
                            ResponseTransforms = route.ResponseTransforms.Select(transform => transform switch {
                                ResponseHeaderTransformExportV1_24 header => (ResponseTransformExportV1_25)new ResponseHeaderTransformExportV1_25 {
                                    Id = header.Id,
                                    RouteId = header.RouteId,
                                    Operation = header.Operation,
                                    HeaderName = header.HeaderName,
                                    HeaderValue = header.HeaderValue
                                },
                                _ => throw new NotSupportedException($"No upgrade mapping for {transform.GetType().Name}")
                            }).ToList()
                        };
                    }).ToList(),

                    Clusters = Clusters?.Select(cluster => new ClusterExportV1_25 {
                        Id = cluster.Id,
                        Name = cluster.Name,
                        Description = cluster.Description,
                        Destinations = cluster.Destinations.Select(destination => new DestinationExportV1_25 {
                            Id = destination.Id,
                            Name = destination.Name,
                            ClusterId = destination.ClusterId,
                            Order = destination.Order,
                            Address = destination.Address,
                            Health = destination.Health
                        }).ToList(),
                        LoadBalancing = cluster.LoadBalancing,
                        StickySessions = cluster.StickySessions,
                        AcceptAnyCert = cluster.AcceptAnyCert
                    }).ToList(),

                    SniMaps = SniMaps?.Select(map => new SniMapExportV1_25 {
                        Host = map.Host,
                        CertificateId = map.CertificateId
                    }).ToList(),

                    AcmeAccounts = AcmeAccounts?.Select(account => new AcmeAccountExportV1_25 {
                        Id = account.Id,
                        Name = account.Name,
                        Provider = account.Provider,
                        Url = account.Url,
                        Hidden = account.Hidden,
                        Key = account.Key
                    }).ToList(),

                    AcmeDnsProviders = AcmeDnsProviders?.Select(provider => new AcmeDnsProviderExportV1_25 {
                        Id = provider.Id,
                        Name = provider.Name,
                        Script = provider.Script
                    }).ToList(),

                    CertificateAuthorities = CertificateAuthorities?.Select(ca => new CertificateAuthorityExportV1_25 {
                        Id = ca.Id,
                        Name = ca.Name,
                        Hidden = ca.Hidden,
                        Material = ca.Material
                    }).ToList(),

                    ServerCertificates = ServerCertificates?.Select(cert => cert switch {
                        IssuedCertExportV1_24 issued => (ServerCertExportV1_25)new IssuedCertExportV1_25 {
                            Id = issued.Id,
                            Name = issued.Name,
                            Hidden = issued.Hidden,
                            Material = issued.Material,
                            IssuingCAId = issued.IssuingCAId
                        },
                        AcmeCertExportV1_24 acme => new AcmeCertExportV1_25 {
                            Id = acme.Id,
                            Name = acme.Name,
                            Hidden = acme.Hidden,
                            Material = acme.Material,
                            AcmeAccountId = acme.AcmeAccountId,
                            LastAttempt = acme.LastAttempt,
                            Hosts = acme.Hosts.Select(host => host switch {
                                AcmeHttpCertHostExportV1_24 http => (AcmeCertHostExportV1_25)new AcmeHttpCertHostExportV1_25 {
                                    Host = http.Host
                                },
                                AcmeDnsCertHostExportV1_24 dns => new AcmeDnsCertHostExportV1_25 {
                                    Host = dns.Host,
                                    DnsProviderId = dns.DnsProviderId
                                },
                                _ => throw new NotSupportedException($"No upgrade mapping for {host.GetType().Name}")
                            }).ToList()
                        },
                        ImportedCertExportV1_24 imported => new ImportedCertExportV1_25 {
                            Id = imported.Id,
                            Name = imported.Name,
                            Hidden = imported.Hidden,
                            Material = imported.Material
                        },
                        _ => throw new NotSupportedException($"No upgrade mapping for {cert.GetType().Name}")
                    }).ToList()
                };
            }
        }

        public class SystemSettingsExportV1_24 {
            public bool AdminListenAny { get; set; }
            public int AdminPort { get; set; }
            public Guid? AdminCertId { get; set; }
            public Guid? FallbackCertId { get; set; }
            public long? MaxRequestBodySize { get; set; }
            public int UnmatchedStatus { get; set; }
            public bool Tarpit { get; set; }

        }

        public class RouteExportV1_24 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Description { get; set; }
            public bool Disabled { get; set; }
            public int Order { get; set; }
            public required string ResponseType { get; set; }
            public Guid? ClusterId { get; set; }
            public int? HttpStatusCode { get; set; }
            public string? RedirectLocation { get; set; }
            public List<string>? Methods { get; set; }
            public List<string>? Hosts { get; set; }
            public required List<RouteMatchExportV1_24> MatchCriteria { get; set; }
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
            public required List<RequestTransformExportV1_24> RequestTransforms { get; set; }
            public string? StaticResponseBody { get; set; }
            public Dictionary<string, string>? StaticResponseHeaders { get; set; }
            public bool PreserveResponseHeaders { get; set; } = true;
            public List<string>? AllowedResponseHeaders { get; set; }
            public List<ResponseTransformExportV1_24> ResponseTransforms { get; set; } = new();

        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(RequestPathTransformExportV1_24), "Path")]
        [JsonDerivedType(typeof(RequestMethodTransformExportV1_24), "Method")]
        [JsonDerivedType(typeof(RequestHeaderTransformExportV1_24), "Header")]
        [JsonDerivedType(typeof(RequestQueryTransformExportV1_24), "Query")]
        public abstract class RequestTransformExportV1_24 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
            public abstract RequestTransformExport ToCurrent();
        }

        public class RequestPathTransformExportV1_24 : RequestTransformExportV1_24 {
            public required string Mode { get; set; }
            public required string PathString { get; set; }

            public override RequestTransformExport ToCurrent() => new RequestPathTransformExport {
                Id = Id,
                RouteId = RouteId,
                Mode = Mode,
                PathString = PathString
            };
        }

        public class RequestMethodTransformExportV1_24 : RequestTransformExportV1_24 {
            public required string FromHttpMethod { get; set; }
            public required string ToHttpMethod { get; set; }

            public override RequestTransformExport ToCurrent() => new RequestMethodTransformExport {
                Id = Id,
                RouteId = RouteId,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }

        public class RequestHeaderTransformExportV1_24 : RequestTransformExportV1_24 {
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

        public class RequestQueryTransformExportV1_24 : RequestTransformExportV1_24 {
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
        [JsonDerivedType(typeof(ResponseHeaderTransformExportV1_24), "Header")]
        public abstract class ResponseTransformExportV1_24 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }

            public abstract ResponseTransformExport ToCurrent();
        }

        public class ResponseHeaderTransformExportV1_24 : ResponseTransformExportV1_24 {
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
        [JsonDerivedType(typeof(RouteHeaderMatchExportV1_24), "Header")]
        [JsonDerivedType(typeof(RouteQueryMatchExportV1_24), "Query")]
        public abstract class RouteMatchExportV1_24 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
            public required string Key { get; set; }
            public List<string>? Values { get; set; }
            public bool? CaseSensitiveValues { get; set; }
            public abstract RouteMatchExport ToCurrent();
        }
        public class RouteHeaderMatchExportV1_24 : RouteMatchExportV1_24 {
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
        public class RouteQueryMatchExportV1_24 : RouteMatchExportV1_24 {
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

        public class ClusterExportV1_24 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Description { get; set; }
            public required List<DestinationExportV1_24> Destinations { get; set; }
            public string? LoadBalancing { get; set; }
            public bool StickySessions { get; set; }
            public bool AcceptAnyCert { get; set; }

        }

        public class DestinationExportV1_24 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public Guid ClusterId { get; set; }
            public int Order { get; set; }
            public required string Address { get; set; }
            public string? Health { get; set; }

        }

        public class SniMapExportV1_24 {
            public required string Host { get; set; }
            public Guid CertificateId { get; set; }

        }

        public class AcmeAccountExportV1_24 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Provider { get; set; }
            public required string Url { get; set; }
            public bool Hidden { get; set; }
            public required string Key { get; set; }

        }

        public class AcmeDnsProviderExportV1_24 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public string? Script { get; set; }

        }

        public class CertificateAuthorityExportV1_24 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public bool Hidden { get; set; }
            public required string Material { get; set; }

        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(IssuedCertExportV1_24), "Issued")]
        [JsonDerivedType(typeof(AcmeCertExportV1_24), "ACME")]
        [JsonDerivedType(typeof(ImportedCertExportV1_24), "Imported")]
        public abstract class ServerCertExportV1_24 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public bool Hidden { get; set; }
            public required string Material { get; set; }
        }

        public class IssuedCertExportV1_24 : ServerCertExportV1_24 {
            public Guid IssuingCAId { get; set; }
        }

        public class AcmeCertExportV1_24 : ServerCertExportV1_24 {
            public Guid AcmeAccountId { get; set; }
            public DateTime? LastAttempt { get; set; }
            public required List<AcmeCertHostExportV1_24> Hosts { get; set; }
        }

        public class ImportedCertExportV1_24 : ServerCertExportV1_24 {
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(AcmeHttpCertHostExportV1_24), AcmeChallengeType.HTTP_01)]
        [JsonDerivedType(typeof(AcmeDnsCertHostExportV1_24), AcmeChallengeType.DNS_01)]
        public abstract class AcmeCertHostExportV1_24 {
            public required string Host { get; set; }
        }

        public class AcmeHttpCertHostExportV1_24 : AcmeCertHostExportV1_24 {
        }

        public class AcmeDnsCertHostExportV1_24 : AcmeCertHostExportV1_24 {
            public Guid DnsProviderId { get; set; }

        }
    }
}
