namespace ByProxy.Models
{
    public abstract class ProxyCert {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public bool Hidden { get; set; }

        [NotMapped]
        public abstract CertType CertType { get; }

        internal async Task<string> ReadMaterialAsBase64Async(CancellationToken cancellationToken) {
            byte[] bytes = await File.ReadAllBytesAsync(Certificates.GetCertFilePath(Id), cancellationToken);
            return Convert.ToBase64String(bytes);
        }
    }

    public class CACert : ProxyCert {
        public override CertType CertType => CertType.CA;
        public CACert() { }
        public CACert(string caName) { Name = caName; }

        public async Task<CertificateAuthorityExport> ToExportAsync(CancellationToken cancellationToken = default) {
            return new CertificateAuthorityExport {
                Id = Id,
                Name = Name,
                Hidden = Hidden,
                Material = await ReadMaterialAsBase64Async(cancellationToken)
            };
        }
    }

    public class CertificateAuthorityExport {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool Hidden { get; set; }
        public string Material { get; set; }
    }

    public abstract class ServerCert : ProxyCert {
        public abstract Task<ServerCertExport> ToExportAsync(CancellationToken cancellationToken = default);
    }
    
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(IssuedCertExport), "Issued")]
    [JsonDerivedType(typeof(AcmeCertExport), "ACME")]
    [JsonDerivedType(typeof(ImportedCertExport), "Imported")]
    public abstract class ServerCertExport {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool Hidden { get; set; }
        public string Material { get; set; }
    }

    public class IssuedCert : ServerCert {
        public override CertType CertType => CertType.Issued;
        
        public Guid IssuingCAId { get; set; }
        public CACert IssuingCA { get; set; }
        
        public IssuedCert() { }
        public IssuedCert(Guid issuingCaId, string certName) { IssuingCAId = issuingCaId; Name = certName; }
        public override async Task<ServerCertExport> ToExportAsync(CancellationToken cancellationToken = default) {
            return new IssuedCertExport {
                Id = Id,
                Name = Name,
                Hidden = Hidden,
                IssuingCAId = IssuingCAId,
                Material = await ReadMaterialAsBase64Async(cancellationToken)
            };
        }
    }

    public class IssuedCertExport : ServerCertExport {
        public Guid IssuingCAId { get; set; }
    }

    public class AcmeCert : ServerCert {
        public override CertType CertType => CertType.Acme;
        public Guid AcmeAccountId { get; set; }
        public AcmeAccount AcmeAccount { get; set; }

        public DateTime? LastAttempt { get; set; }
        public List<AcmeCertHost> Hosts { get; set; }

        public override async Task<ServerCertExport> ToExportAsync(CancellationToken cancellationToken = default) {
            return new AcmeCertExport {
                Id = Id,
                Name = Name,
                Hidden = Hidden,
                AcmeAccountId = AcmeAccountId,
                LastAttempt = LastAttempt,
                Hosts = Hosts.Select(_ => _.ToExport()).ToList(),
                Material = await ReadMaterialAsBase64Async(cancellationToken)
            };
        }
    }

    public class AcmeCertExport : ServerCertExport {
        public Guid AcmeAccountId { get; set; }
        public DateTime? LastAttempt { get; set; }
        public List<AcmeCertHostExport> Hosts { get; set; }
    }

    public class ImportedCert : ServerCert {
        public override CertType CertType => CertType.Imported;
        
        public ImportedCert() { }
        public ImportedCert(string certName) { Name = certName; }

        public override async Task<ServerCertExport> ToExportAsync(CancellationToken cancellationToken = default) {
            return new ImportedCertExport {
                Id = Id,
                Name = Name,
                Hidden = Hidden,
                Material = await ReadMaterialAsBase64Async(cancellationToken)
            };
        }
    }

    public class ImportedCertExport : ServerCertExport { }

    public record CertType(string Type) {
        private const string _ca = "CA";
        private const string _acme = "ACME";
        private const string _issued = "Issued";
        private const string _imported = "Imported";

        public static class Constants {
            public const string Discriminator = "Type";
        }

        public static readonly CertType CA = new(_ca);
        public static readonly CertType Acme = new(_acme);
        public static readonly CertType Issued = new(_issued);
        public static readonly CertType Imported = new(_imported);

        public static CertType? FromString(string? value) => value switch {
            _ca => CA,
            _acme => Acme,
            _issued => Issued,
            _imported => Imported,
            _ => null
        };

        public override string ToString() {
            return Type;
        }

        public class CertTypeConverter : ValueConverter<CertType?, string?> {
            public CertTypeConverter() : base(
                certType => certType == null ? null : certType.Type,
                    value => FromString(value)) { }
        }
    }
}
