using System.Formats.Asn1;

namespace ByProxy.Utility {
    public static class Certificates {
        public static readonly ImmutableList<string> LoopbackHosts = ["localhost", IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString()];

        private static string GetCertPath(Guid certId) => Path.Combine(AppPaths.CertDir, $"{certId}.pfx");

        public static void DisposeCollection(this X509Certificate2Collection collection) {
            foreach(var cert in collection) {
                cert.Dispose();
            }
            collection.Clear();
        }

        public static X509Certificate2 ImportCertFromDisk(Guid certId) {
            var filePath = GetCertPath(certId);
            X509KeyStorageFlags keyFlags = X509KeyStorageFlags.Exportable;
            if (OperatingSystem.IsWindows()) {
                keyFlags |= X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet;
            } else {
                keyFlags |= X509KeyStorageFlags.EphemeralKeySet;
            }
            return new X509Certificate2(filePath, (string?)null, keyFlags);
        }

        public static void ExportCertToDisk(Guid certId, X509Certificate2 x509Cert) {
            File.WriteAllBytes(GetCertPath(certId), x509Cert.Export(X509ContentType.Pfx, (string?)null));
        }

        public static void ExportCertToDisk(Guid certId, X509Certificate2Collection x509Collection) {
            byte[]? pfxBytes = x509Collection.Export(X509ContentType.Pfx);
            if (pfxBytes == null) throw new Exception("Unknown error exporting certificate collection.");
            File.WriteAllBytes(GetCertPath(certId), pfxBytes);
        }

        public static void DeleteCertFromDisk(Guid certId) {
            File.Delete(GetCertPath(certId));
        }


        public static X509Certificate2Collection ImportPemChainAndKey(string pem, string key) {
            var chain = new X509Certificate2Collection();
            chain.ImportFromPem(pem);
            var endPem = chain[0].ExportCertificatePem();
            var endCert = X509Certificate2.CreateFromPem(endPem, key);
            return ImportPemChainAndKey(chain, endCert);
        }

        public static X509Certificate2Collection ImportPemChainAndKey(string pem, RSA key) {
            var chain = new X509Certificate2Collection();
            chain.ImportFromPem(pem);
            var endCert = chain[0].CopyWithPrivateKey(key);
            return ImportPemChainAndKey(chain, endCert);
        }

        public static X509Certificate2Collection ImportPemChainAndKey(string pem, ECDsa key) {
            var chain = new X509Certificate2Collection();
            chain.ImportFromPem(pem);
            var endCert = chain[0].CopyWithPrivateKey(key);
            return ImportPemChainAndKey(chain, endCert);
        }

        private static X509Certificate2Collection ImportPemChainAndKey(X509Certificate2Collection importedChain, X509Certificate2 endCertWithKey) {
            var chain = new X509Certificate2Collection(endCertWithKey);
            for (int i = 1; i < importedChain.Count; i++) {
                chain.Add(importedChain[i]);
            }
            importedChain[0].Dispose();
            return chain;
        }

        public static X509Certificate2 GenerateSelfSignedCert(IList<string> hosts, int validDays = 3650) {
            var cn = hosts[0];
            if (hosts.Count == 1) return GenerateSelfSignedCert(cn);

            var dnsNames = new HashSet<string>();
            var ipAddresses = new HashSet<IPAddress>();
            foreach (var host in hosts.Skip(1)) {
                if (IPAddress.TryParse(host, out var ip)) {
                    ipAddresses.Add(ip);
                } else {
                    dnsNames.Add(host);
                }
            }
            return GenerateSelfSignedCert(cn, dnsNames, ipAddresses, validDays);
        }

        public static X509Certificate2 GenerateSelfSignedCert(
            string commonName,
            IEnumerable<string>? dnsNames = null,
            IEnumerable<IPAddress>? ipAddresses = null,
            int validDays = 3650
        ) {
            using var rsa = RSA.Create(2048);

            var req = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

            var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // Server Authentication
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));

            var san = new SubjectAlternativeNameBuilder();
            if (IPAddress.TryParse(commonName, out var cnIp)) {
                san.AddIpAddress(cnIp);
            } else {
                san.AddDnsName(commonName);
            }
            if (dnsNames != null) {
                foreach (var name in dnsNames) {
                    if (!string.IsNullOrWhiteSpace(name)) san.AddDnsName(name.Trim());
                }
            }
            if (ipAddresses != null) {
                foreach (var ip in ipAddresses) {
                    san.AddIpAddress(ip);
                }
            }
            req.CertificateExtensions.Add(san.Build());

            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = notBefore.AddDays(validDays);

            return req.CreateSelfSigned(notBefore, notAfter);
        }

        public static X509Certificate2 CreateCertificateAuthority(string name) {
            using var rsa = RSA.Create(2048);
            var subject = new X500DistinguishedName($"CN={name}");
            var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = notBefore.AddYears(10);
            return req.CreateSelfSigned(notBefore, notAfter);
        }

        public static X509Certificate2 IssueServerCertFromCA(X509Certificate2 caCert, IList<string> hosts) {
            var cn = hosts[0];
            if (hosts.Count == 1) return IssueServerCertFromCA(caCert, cn);

            var dnsNames = new HashSet<string>();
            var ipAddresses = new HashSet<IPAddress>();
            foreach (var host in hosts.Skip(1)) {
                if (IPAddress.TryParse(host, out var ip)) {
                    ipAddresses.Add(ip);
                } else {
                    dnsNames.Add(host);
                }
            }
            return IssueServerCertFromCA(caCert, cn, dnsNames, ipAddresses);
        }

        public static X509Certificate2 IssueServerCertFromCA(
            X509Certificate2 caCert,
            string commonName,
            IEnumerable<string>? dnsNames = null,
            IEnumerable<IPAddress>? ipAddresses = null
        ) {
            using var caKey = caCert.GetRSAPrivateKey() ?? throw new InvalidOperationException("CA private key not available.");
            var generator = X509SignatureGenerator.CreateForRSA(caKey, RSASignaturePadding.Pkcs1);

            using var rsa = RSA.Create(2048);

            var subject = new X500DistinguishedName($"CN={commonName}");
            var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));

            var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // Server Authentication
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName(commonName);
            if (dnsNames != null) {
                foreach (var name in dnsNames) {
                    if (!string.IsNullOrWhiteSpace(name)) san.AddDnsName(name.Trim());
                }
            }
            if (ipAddresses != null) {
                foreach (var ip in ipAddresses) {
                    san.AddIpAddress(ip);
                }
            }
            req.CertificateExtensions.Add(san.Build());

            var serial = RandomNumberGenerator.GetBytes(16);
            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = caCert.NotAfter;

            return req.Create(caCert.SubjectName, generator, notBefore, notAfter, serial).CopyWithPrivateKey(rsa);
        }

        public static List<string> GetSubjectAltNames(X509Certificate2 cert) {
            var result = new List<string>();

            foreach (var ext in cert.Extensions) {
                if (ext.Oid?.Value != "2.5.29.17") continue;

                var reader = new AsnReader(ext.RawData, AsnEncodingRules.DER);
                var seq = reader.ReadSequence();

                while (seq.HasData) {
                    var tag = seq.PeekTag();

                    if (tag.TagClass != TagClass.ContextSpecific) {
                        seq.ReadEncodedValue();
                        continue;
                    }

                    switch (tag.TagValue) {
                        case 2: { // DNS Name
                                var dns = seq.ReadCharacterString(
                                    UniversalTagNumber.IA5String,
                                    new Asn1Tag(TagClass.ContextSpecific, 2)
                                );
                                if (!string.IsNullOrWhiteSpace(dns))
                                    result.Add(dns);
                                break;
                            }
                        case 7: { // IP Address
                                var ipBytes = seq.ReadOctetString(
                                    new Asn1Tag(TagClass.ContextSpecific, 7)
                                );
                                if (ipBytes is { Length: > 0 }) {
                                    var ip = new IPAddress(ipBytes);
                                    result.Add(ip.ToString());
                                }
                                break;
                            }
                        default:
                            seq.ReadEncodedValue();
                            break;
                    }
                }
                break;
            }
            return result;
        }

        public static List<string> GetCertIssues(ProxyCert metadata, X509Certificate2 cert, string? host = null) {
            var certIssues = new List<string>();
            if (metadata.Hidden) certIssues.Add("Inactive Certificate");
            if (cert.NotBefore > DateTime.Now) certIssues.Add("Not Yet Valid");
            if (cert.NotAfter < DateTime.Now) certIssues.Add("Expired");

            if (host == null) return certIssues;

            var subjectNames = GetSubjectAltNames(cert);
            if (
                !subjectNames.Contains(host, StringComparer.OrdinalIgnoreCase)
                && !subjectNames.Contains($"*.{host}", StringComparer.OrdinalIgnoreCase)
            ) {
                certIssues.Add("Subject Not Present");
            }
            return certIssues;
        }

        public static string GetFingerprint(X509Certificate2 cert) =>
            Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLower();
    }
}
