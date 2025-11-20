namespace ByProxy.Models
{
    public abstract class ProxyCert {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public bool Hidden { get; set; }

        [NotMapped]
        public abstract CertType CertType { get; }
    }

    public class CACert : ProxyCert {
        public override CertType CertType => CertType.CA;
        public CACert() { }
        public CACert(string caName) { Name = caName; }
    }

    public abstract class ServerCert : ProxyCert { }

    public class IssuedCert : ServerCert {
        public override CertType CertType => CertType.Issued;
        
        public Guid IssuingCAId { get; set; }
        public CACert IssuingCA { get; set; }
        
        public IssuedCert() { }
        public IssuedCert(Guid issuingCaId, string certName) { IssuingCAId = issuingCaId; Name = certName; }
    }

    public class AcmeCert : ServerCert {
        public override CertType CertType => CertType.Acme;
        public Guid AcmeAccountId { get; set; }
        public AcmeAccount AcmeAccount { get; set; }

        public DateTime? LastAttempt { get; set; }
        public List<AcmeCertHost> Hosts { get; set; }
    }

    public class ImportedCert : ServerCert {
        public override CertType CertType => CertType.Imported;
        
        public ImportedCert() { }
        public ImportedCert(string certName) { Name = certName; }
    }

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
