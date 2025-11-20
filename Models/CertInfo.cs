namespace ByProxy.Models {
    public record CertInfo(
        ProxyCert Metadata,
        X509Certificate2 Cert,
        List<string> SubjectNames,
        string Issuer,
        List<string> SniHosts
    );
}
