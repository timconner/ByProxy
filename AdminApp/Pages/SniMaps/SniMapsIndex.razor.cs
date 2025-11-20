namespace ByProxy.AdminApp.Pages.SniMaps;
public partial class SniMapsIndex {
    private List<EnrichedSniMap>? _sniMaps = null;

    private ServerCert? _fallbackCert = null;

    private sealed record EnrichedSniMap(
        ProxySniMap SniMap,
        ServerCert CertMetadata,
        X509Certificate2 Cert,
        List<string> CertIssues
    );

    protected override async Task OnInitializedAsync() {
        await ReloadMaps();
    }

    private async Task ReloadMaps() {
        var db = _dbFactory.CreateDbContext();

        var sniMaps = await db.SniMaps
            .AsNoTracking()
            .ToListAsync();

        var mapCerts = await db.ServerCerts
            .AsNoTracking()
            .IgnoreQueryFilters() // Get inactive certs if mapped
            .Where(cert => sniMaps.Select(map => map.CertificateId).Contains(cert.Id))
            .ToListAsync();

        _fallbackCert = await db.Configurations
            .AsNoTracking()
            .IgnoreQueryFilters() // in case current fallback is inactive
            .Where(_ => _.Revision == _proxyState.CandidateConfigRevision)
            .Select(_ => _.FallbackCert)
            .FirstOrDefaultAsync();

        var enriched = new List<EnrichedSniMap>();
        foreach (var map in sniMaps.OrderByHost()) {
            var metadata = mapCerts.First(_ => _.Id == map.CertificateId);
            var cert = _certService.GetCertificate(map.CertificateId);
            var certIssues = Certificates.GetCertIssues(metadata, cert, map.Host);

            enriched.Add(new EnrichedSniMap(map, metadata, cert, certIssues));
        }
        _sniMaps = enriched;
    }

    private async Task ViewCertDetails(Guid certId) {
        await _modal.CertificateDetails(certId);
    }

    private async Task ChangeMapCert(EnrichedSniMap map) {
        var result = await _modal.CertificatePicker($"{_strings["Change SNI Map Certificate"]}  |  {map.SniMap.Host}");
        if (result.DialogResult != DialogResult.OK || result.Value == null) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            var entry = db.Attach(map.SniMap);
            entry.Entity.CertificateId = result.Value.Id;
            await db.SaveChangesAsync();
        });
        if (success) {
            _ = _config.UpdateHasChangesPending();
            await ReloadMaps();
        }
    }

    private async Task DeleteMap(EnrichedSniMap map) {
        var result = await _modal.YesNo(_strings["Delete"], $"{_strings["Delete"]}: {map.SniMap.Host}\n\n{_strings["WARN_DeleteMap"]}");
        if (result != DialogResult.Yes) return;

        var success = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();
            db.Attach(map.SniMap);
            db.SniMaps.Remove(map.SniMap);
            await db.SaveChangesAsync();
        });

        if (success) {
            _ = _config.UpdateHasChangesPending();
            await ReloadMaps();
        }
    }

}
