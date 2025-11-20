namespace ByProxy.AdminApp.Pages.SniMaps;
public partial class AddSniMap {
    private record NewMap(
        string Host,
        CertInfo CertInfo,
        List<string> CertIssues
    );

    private List<NewMap> _newMaps = new();
    private List<ProxySniMap> _currentMaps = new();

    private List<CertInfo>? _availableCerts;
    private readonly Dictionary<string, HashSet<CertInfo>> _hostLookup = new();

    private string _newHost = string.Empty;
    private System.Threading.Timer _hostInputDebounce = default!;
    private HashSet<CertInfo>? _matchingCerts;

    protected override async Task OnInitializedAsync() {
        _hostInputDebounce = new System.Threading.Timer(UpdateHostMatches, null, Timeout.Infinite, Timeout.Infinite);

        using var db = _dbFactory.CreateDbContext();
        _currentMaps = await db.SniMaps
                .AsNoTracking()
                .ToListAsync();

        var availableCerts = await _certService.CompileAvailableServerCerts(_strings);
        foreach (var cert in availableCerts) {
            foreach (var name in cert.SubjectNames) {
                if (_hostLookup.TryGetValue(name, out var certs)) {
                    certs.Add(cert);
                } else {
                    _hostLookup.Add(name, new() { cert });
                }
            }
        }

        _availableCerts = availableCerts;
    }

    private void HostInput(ChangeEventArgs e) {
        _newHost = e.Value?.ToString() ?? string.Empty;
        _matchingCerts = null;
        _hostInputDebounce.Change(300, Timeout.Infinite);
    }

    private async void UpdateHostMatches(object? state) {
        var matches = new HashSet<CertInfo>();
        if (_hostLookup.TryGetValue(_newHost, out var hostMatches)) {
            matches.UnionWith(hostMatches);
        }
        if (_newHost.Contains('.') && !IPAddress.TryParse(_newHost, out _)) {
            var hostParts = _newHost.Split('.', 2);
            if (hostParts.Length == 2) {
                var wildcard = $"*.{hostParts[1]}";
                if (_hostLookup.TryGetValue(wildcard, out var wildcardMatches)) {
                    matches.UnionWith(wildcardMatches);
                }
            }
        }
        _matchingCerts = matches;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ViewFingerprints(CertInfo certInfo) {
        await _modal.CertificateDetails(certInfo, true);
    }

    private async Task AddMap(CertInfo certInfo) {
        if (string.IsNullOrWhiteSpace(_newHost)) return;
        var host = _newHost.Trim();

        if (_currentMaps.Any(_ => _.Host == host)) {
            var result = await _modal.YesNo("Overwrite", $"An SNI map already exists for: {host}\n\nAdding this map will overwrite the existing one on Save.\n\nContinue?");
            if (result != DialogResult.Yes) return;
        }

        var certIssues = Certificates.GetCertIssues(certInfo.Metadata, certInfo.Cert, _newHost);
        _newMaps.Add(new NewMap(_newHost, certInfo, certIssues));
        _newHost = string.Empty;
    }

    private void RemoveMap(NewMap map) {
        _newMaps.Remove(map);
        HostInput(new ChangeEventArgs { Value = map.Host });
    }

    private async Task PerformSave() {
        if (_newMaps.Count == 0) return;

        if (_newMaps.Any(_ => _.CertIssues.Count > 0)) {
            var result = await _modal.YesNo("Problems Detected", "One or more of the new maps have certificate issues. Are you sure you want to add them?");
            if (result != DialogResult.Yes) return;
        }

        var saveSuccessful = await _modal.PerformSave(async () => {
            using var db = _dbFactory.CreateDbContext();

            var currentMaps = await db.SniMaps.ToListAsync();
            foreach (var map in _newMaps) {
                var currentMap = currentMaps.FirstOrDefault(_ => _.Host == map.Host);
                if (currentMap != null) {
                    currentMap.CertificateId = map.CertInfo.Metadata.Id;
                } else {
                    db.SniMaps.Add(new ProxySniMap {
                        ConfigRevision = _proxyState.CandidateConfigRevision,
                        Host = map.Host,
                        CertificateId = map.CertInfo.Metadata.Id
                    });
                }
            }

            await db.SaveChangesAsync();
        });

        if (saveSuccessful) {
            _ = _config.UpdateHasChangesPending();
            _nav.NavigateTo("/sni-maps");
        }
    }
}
