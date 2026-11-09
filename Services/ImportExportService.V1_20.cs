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
            public override ProxyExport ToNext() {
                return new ProxyExportV1_24 {
                    SystemSettings = SystemSettings == null ? null : new SystemSettingsExportV1_24 {
                        AdminListenAny = SystemSettings.AdminListenAny,
                        AdminPort = SystemSettings.AdminPort,
                        AdminCertId = SystemSettings.AdminCertId,
                        FallbackCertId = SystemSettings.FallbackCertId,
                        MaxRequestBodySize = SystemSettings.MaxRequestBodySize,
                        UnmatchedStatus = SystemSettings.UnmatchedStatus,
                        Tarpit = SystemSettings.Tarpit
                    },

                    Routes = Routes?.Select(route => new RouteExportV1_24 {
                        Id = route.Id,
                        Name = route.Name,
                        Description = route.Description,
                        Disabled = route.Disabled,
                        Order = route.Order,
                        ResponseType = route.ResponseType == "Status" ? "Static" : route.ResponseType,
                        ClusterId = route.ClusterId,
                        HttpStatusCode = route.HttpStatusCode,
                        RedirectLocation = route.RedirectLocation,
                        Methods = route.Methods?.ToList(),
                        Hosts = route.Hosts?.ToList(),

                        MatchCriteria = route.MatchCriteria.Select(match => match switch {
                            RouteHeaderMatchExportV1_20 header => (RouteMatchExportV1_24)new RouteHeaderMatchExportV1_24 {
                                Id = header.Id,
                                RouteId = header.RouteId,
                                Key = header.Key,
                                Values = header.Values?.ToList(),
                                CaseSensitiveValues = header.CaseSensitiveValues,
                                Mode = header.Mode
                            },
                            RouteQueryMatchExportV1_20 query => new RouteQueryMatchExportV1_24 {
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
                        AllowedRequestHeaders = route.AllowedHeaders?.ToList(),
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

                        RequestTransforms = route.Transforms.Select(transform => transform switch {
                            RoutePathTransformExportV1_20 path => (RequestTransformExportV1_24)new RequestPathTransformExportV1_24 {
                                Id = path.Id,
                                RouteId = path.RouteId,
                                Mode = path.Mode,
                                PathString = path.PathString
                            },
                            RouteMethodTransformExportV1_20 method => new RequestMethodTransformExportV1_24 {
                                Id = method.Id,
                                RouteId = method.RouteId,
                                FromHttpMethod = method.FromHttpMethod,
                                ToHttpMethod = method.ToHttpMethod
                            },
                            RouteHeaderTransformExportV1_20 header => new RequestHeaderTransformExportV1_24 {
                                Id = header.Id,
                                RouteId = header.RouteId,
                                Operation = header.Operation,
                                HeaderName = header.HeaderName,
                                HeaderValue = header.HeaderValue
                            },
                            RouteQueryTransformExportV1_20 query => new RequestQueryTransformExportV1_24 {
                                Id = query.Id,
                                RouteId = query.RouteId,
                                Operation = query.Operation,
                                QueryKey = query.QueryKey,
                                QueryValue = query.QueryValue
                            },
                            _ => throw new NotSupportedException($"No upgrade mapping for {transform.GetType().Name}")
                        }).ToList(),

                        StaticResponseBody = null,
                        StaticResponseHeaders = null,
                        PreserveResponseHeaders = true,
                        AllowedResponseHeaders = null,
                        ResponseTransforms = new()
                    }).ToList(),

                    Clusters = Clusters?.Select(cluster => new ClusterExportV1_24 {
                        Id = cluster.Id,
                        Name = cluster.Name,
                        Description = cluster.Description,
                        Destinations = cluster.Destinations.Select(destination => new DestinationExportV1_24 {
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

                    SniMaps = SniMaps?.Select(map => new SniMapExportV1_24 {
                        Host = map.Host,
                        CertificateId = map.CertificateId
                    }).ToList(),

                    AcmeAccounts = AcmeAccounts?.Select(account => new AcmeAccountExportV1_24 {
                        Id = account.Id,
                        Name = account.Name,
                        Provider = account.Provider,
                        Url = account.Url,
                        Hidden = account.Hidden,
                        Key = account.Key
                    }).ToList(),

                    AcmeDnsProviders = AcmeDnsProviders?.Select(provider => new AcmeDnsProviderExportV1_24 {
                        Id = provider.Id,
                        Name = provider.Name,
                        Script = provider.Script
                    }).ToList(),

                    CertificateAuthorities = CertificateAuthorities?.Select(ca => new CertificateAuthorityExportV1_24 {
                        Id = ca.Id,
                        Name = ca.Name,
                        Hidden = ca.Hidden,
                        Material = ca.Material
                    }).ToList(),

                    ServerCertificates = ServerCertificates?.Select(cert => cert switch {
                        IssuedCertExportV1_20 issued => (ServerCertExportV1_24)new IssuedCertExportV1_24 {
                            Id = issued.Id,
                            Name = issued.Name,
                            Hidden = issued.Hidden,
                            Material = issued.Material,
                            IssuingCAId = issued.IssuingCAId
                        },
                        AcmeCertExportV1_20 acme => new AcmeCertExportV1_24 {
                            Id = acme.Id,
                            Name = acme.Name,
                            Hidden = acme.Hidden,
                            Material = acme.Material,
                            AcmeAccountId = acme.AcmeAccountId,
                            LastAttempt = acme.LastAttempt,
                            Hosts = acme.Hosts.Select(host => host switch {
                                AcmeHttpCertHostExportV1_20 http => (AcmeCertHostExportV1_24)new AcmeHttpCertHostExportV1_24 {
                                    Host = http.Host
                                },
                                AcmeDnsCertHostExportV1_20 dns => new AcmeDnsCertHostExportV1_24 {
                                    Host = dns.Host,
                                    DnsProviderId = dns.DnsProviderId
                                },
                                _ => throw new NotSupportedException($"No upgrade mapping for {host.GetType().Name}")
                            }).ToList()
                        },
                        ImportedCertExportV1_20 imported => new ImportedCertExportV1_24 {
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

        public class SystemSettingsExportV1_20 {
            public bool AdminListenAny { get; set; }
            public int AdminPort { get; set; }
            public Guid? AdminCertId { get; set; }
            public Guid? FallbackCertId { get; set; }
            public long? MaxRequestBodySize { get; set; }
            public int UnmatchedStatus { get; set; }
            public bool Tarpit { get; set; }
        }

        public class RouteExportV1_20 {
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
            public required List<RouteMatchExportV1_20> MatchCriteria { get; set; }
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
            public required List<RouteTransformExportV1_20> Transforms { get; set; }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(RoutePathTransformExportV1_20), "Path")]
        [JsonDerivedType(typeof(RouteMethodTransformExportV1_20), "Method")]
        [JsonDerivedType(typeof(RouteHeaderTransformExportV1_20), "Header")]
        [JsonDerivedType(typeof(RouteQueryTransformExportV1_20), "Query")]
        public abstract class RouteTransformExportV1_20 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
        }

        public class RoutePathTransformExportV1_20 : RouteTransformExportV1_20 {
            public required string Mode { get; set; }
            public required string PathString { get; set; }
        }

        public class RouteMethodTransformExportV1_20 : RouteTransformExportV1_20 {
            public required string FromHttpMethod { get; set; }
            public required string ToHttpMethod { get; set; }
        }

        public class RouteHeaderTransformExportV1_20 : RouteTransformExportV1_20 {
            public required string Operation { get; set; }
            public required string HeaderName { get; set; }
            public string? HeaderValue { get; set; }
        }

        public class RouteQueryTransformExportV1_20 : RouteTransformExportV1_20 {
            public required string Operation { get; set; }
            public required string QueryKey { get; set; }
            public string? QueryValue { get; set; }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(RouteHeaderMatchExportV1_20), "Header")]
        [JsonDerivedType(typeof(RouteQueryMatchExportV1_20), "Query")]
        public abstract class RouteMatchExportV1_20 {
            public Guid Id { get; set; }
            public Guid RouteId { get; set; }
            public required string Key { get; set; }
            public List<string>? Values { get; set; }
            public bool? CaseSensitiveValues { get; set; }
        }
        public class RouteHeaderMatchExportV1_20 : RouteMatchExportV1_20 {
            public required string Mode { get; set; }
        }
        public class RouteQueryMatchExportV1_20 : RouteMatchExportV1_20 {
            public required string Mode { get; set; }
        }

        public class ClusterExportV1_20 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Description { get; set; }
            public required List<DestinationExportV1_20> Destinations { get; set; }
            public string? LoadBalancing { get; set; }
            public bool StickySessions { get; set; }
            public bool AcceptAnyCert { get; set; }
        }

        public class DestinationExportV1_20 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public Guid ClusterId { get; set; }
            public int Order { get; set; }
            public required string Address { get; set; }
            public string? Health { get; set; }
        }

        public class SniMapExportV1_20 {
            public required string Host { get; set; }
            public Guid CertificateId { get; set; }
        }

        public class AcmeAccountExportV1_20 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public required string Provider { get; set; }
            public required string Url { get; set; }
            public bool Hidden { get; set; }
            public required string Key { get; set; }
        }

        public class AcmeDnsProviderExportV1_20 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public string? Script { get; set; }
        }

        public class CertificateAuthorityExportV1_20 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public bool Hidden { get; set; }
            public required string Material { get; set; }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(IssuedCertExportV1_20), "Issued")]
        [JsonDerivedType(typeof(AcmeCertExportV1_20), "ACME")]
        [JsonDerivedType(typeof(ImportedCertExportV1_20), "Imported")]
        public abstract class ServerCertExportV1_20 {
            public Guid Id { get; set; }
            public required string Name { get; set; }
            public bool Hidden { get; set; }
            public required string Material { get; set; }
        }

        public class IssuedCertExportV1_20 : ServerCertExportV1_20 {
            public Guid IssuingCAId { get; set; }
        }

        public class AcmeCertExportV1_20 : ServerCertExportV1_20 {
            public Guid AcmeAccountId { get; set; }
            public DateTime? LastAttempt { get; set; }
            public required List<AcmeCertHostExportV1_20> Hosts { get; set; }
        }

        public class ImportedCertExportV1_20 : ServerCertExportV1_20 { }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(AcmeHttpCertHostExportV1_20), AcmeChallengeType.HTTP_01)]
        [JsonDerivedType(typeof(AcmeDnsCertHostExportV1_20), AcmeChallengeType.DNS_01)]
        public abstract class AcmeCertHostExportV1_20 {
            public required string Host { get; set; }
        }

        public class AcmeHttpCertHostExportV1_20 : AcmeCertHostExportV1_20 { }

        public class AcmeDnsCertHostExportV1_20 : AcmeCertHostExportV1_20 {
            public Guid DnsProviderId { get; set; }
        }
    }
}
