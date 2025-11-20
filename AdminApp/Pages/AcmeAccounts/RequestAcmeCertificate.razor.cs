namespace ByProxy.AdminApp.Pages.AcmeAccounts;
public partial class RequestAcmeCertificate {
    private AcmeAccount? _account;
    private AcmeProvider? _provider;

    private record DnsProvider(Guid Id, string Name);
    private List<DnsProvider>? _dnsProviders;

    private string _certName = string.Empty;

    private string _newHost = string.Empty;
    private string _newChallengeType = AcmeChallengeType.HTTP_01;
    private Guid? _newDnsProvider;

    private struct ChallengeHost {
        public string Host { get; init; }
        public string ChallengeType { get; init; }
        public Guid? DnsProviderId { get; init; }
        public string? DnsProviderName { get; init; }

        public ChallengeHost(string host, string challengeType) {
            Host = host; ChallengeType = challengeType;
        }

        public ChallengeHost(string host, string challengeType, Guid dnsProviderId, string dnsProviderName) {
            Host = host; ChallengeType = challengeType;
            DnsProviderId = dnsProviderId; DnsProviderName = dnsProviderName;
        }
    }
    private List<ChallengeHost> _acmeHosts = new();

    protected override async Task OnInitializedAsync() {
        using var db = _dbFactory.CreateDbContext();
        _account = await db.AcmeAccounts
            .AsNoTracking()
            .FirstAsync(_ => _.Id == AccountId);

        _provider = _acmeClient.GetProvider(_account.Provider);

        _dnsProviders = await db.AcmeDnsProviders
            .AsNoTracking()
            .Select(_ => new DnsProvider(_.Id, _.Name))
            .ToListAsync();
    }

    private void CheckWildcard() {
        if (_newHost.StartsWith('*')) {
            _newChallengeType = AcmeChallengeType.DNS_01;
        }
    }

    private bool IsNewDomainValid() {
        if (string.IsNullOrWhiteSpace(_newHost)) return false;
        if (_newChallengeType == AcmeChallengeType.DNS_01 && _newDnsProvider == null) return false;
        return true;
    }

    private void AddHost() {
        if (!IsNewDomainValid()) return;
        var newHost = _newHost.Trim();

        if (_acmeHosts.Any(_ => string.Equals(_.Host, newHost, StringComparison.OrdinalIgnoreCase))) return;

        if (_newChallengeType == AcmeChallengeType.DNS_01) {
            var dnsProviderName = _dnsProviders!.First(_ => _.Id == _newDnsProvider).Name;
            _acmeHosts.Add(new ChallengeHost(newHost, _newChallengeType, _newDnsProvider!.Value, dnsProviderName));
        } else {
            _acmeHosts.Add(new ChallengeHost(newHost, _newChallengeType));
        }

        _newHost = string.Empty;
    }

    private void RemoveHost(ChallengeHost host) {
        _acmeHosts.Remove(host);
        _newHost = host.Host;
        _newChallengeType = host.ChallengeType;
    }

    private async Task IssueCert() {
        if (_account == null) return;

        if (string.IsNullOrWhiteSpace(_certName)) {
            await _modal.OkOnly(_strings["Error"], _strings["ERR_NameEmpty"]);
            return;
        }

        if (_acmeHosts.Count == 0) {
            await _modal.OkOnly(_strings["Error"], _strings["ERR_HostsRequired"]);
            return;
        }

        var result = await _modal.YesNo(_strings["Confirm Request"], _strings["Prompt_ConfirmRequest"]);
        if (result != DialogResult.Yes) return;

        try {
            using var db = _dbFactory.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();

            var entry = db.AcmeCerts.Add(new AcmeCert {
                AcmeAccountId = _account.Id,
                Name = _certName,
                Hosts = new()
            });

            foreach (var host in _acmeHosts) {
                switch (host.ChallengeType) {
                    case AcmeChallengeType.HTTP_01:
                        entry.Entity.Hosts.Add(new AcmeHttpCertHost {
                            Host = host.Host
                        });
                        break;
                    case AcmeChallengeType.DNS_01:
                        if (host.DnsProviderId == null) {
                            throw new Exception("Host with DNS challenge type is missing a DNS provider.");
                        }
                        entry.Entity.Hosts.Add(new AcmeDnsCertHost {
                            Host = host.Host,
                            DnsProviderId = host.DnsProviderId.Value
                        });
                        break;
                }
            }

            await db.SaveChangesAsync();

            var success = await _modal.RunWithProgressViewer(_strings["ACME Request Progress"], async (progress) => {
                var localizedProgress = new LocalizingProgress(progress, _strings);
                var certificate = await _acmeClient.RequestNewCertificateAsync(_account, entry.Entity.Hosts, localizedProgress, _proxyState.StoppingOrRestarting);

                Certificates.ExportCertToDisk(entry.Entity.Id, certificate);

                await transaction.CommitAsync();
                certificate.DisposeCollection();
            });

            if (success) _nav.NavigateTo("/acme");
        } catch (Exception ex) {
            await _modal.OkOnly(_strings["Error"], ex.Message);
        }
    }

    private class LocalizingProgress : IProgress<(string Main, string? Detail)> {
        private readonly IProgress<(string Main, string? Detail)> _inner;
        private readonly IStringLocalizer _strings;
        public LocalizingProgress(IProgress<(string Main, string? Detail)> inner, IStringLocalizer strings) {
            _inner = inner;
            _strings = strings;
        }
        public void Report((string Main, string? Detail) value) =>
            _inner.Report((_strings[value.Main].Value, value.Detail));
    }
}
