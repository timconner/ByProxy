namespace ByProxy.AdminApp.Pages.AcmeAccounts;
public partial class EditAcmeCertificate {
    private ProxyDb _db = default!;
    private AcmeCert? _cert;
    private List<AcmeCertHost>? _hosts;
    private AcmeProvider? _provider;
    private Dictionary<Guid, string>? _dnsProviders;

    protected override async Task OnInitializedAsync() {
        _db = _dbFactory.CreateDbContext();
        _cert = await _db.AcmeCerts
            .Include(_ => _.AcmeAccount)
            .Where(_ => _.Id == CertId)
            .FirstAsync();

        _hosts = await _db.AcmeHosts
            .AsNoTracking()
            .Where(_ => _.CertificateId == _cert.Id)
            .ToListAsync();

        _dnsProviders = await _db.AcmeDnsProviders
            .AsNoTracking()
            .ToDictionaryAsync(_ => _.Id, _ => _.Name);

        _provider = _acmeClient.GetProvider(_cert.AcmeAccount.Provider);
    }

    private void ChangeChallengeType(AcmeCertHost host, string? newType) {
        if (_cert == null || string.IsNullOrEmpty(newType) || host.ChallengeType == newType) return;

        switch (newType) {
            case AcmeChallengeType.HTTP_01:
                host = new AcmeHttpCertHost {
                    CertificateId = _cert.Id,
                    Host = host.Host
                };
                break;
            case AcmeChallengeType.DNS_01:
                if (_dnsProviders == null || _dnsProviders.Count == 0) return;
                host = new AcmeDnsCertHost {
                    CertificateId = _cert.Id,
                    Host = host.Host,
                    DnsProviderId = _dnsProviders.First().Key
                };
                break;
        }
    }

    private async Task SaveCert() {
        if (_cert == null || _hosts == null) return;

        if (string.IsNullOrWhiteSpace(_cert.Name)) {
            await _modal.OkOnly(_strings["Error"], _strings["ERR_NameEmpty"]);
            return;
        }

        var success = await _modal.PerformSave(async () => {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();

            await _db.AcmeHosts
                .Where(_ => _.CertificateId == _cert.Id)
                .ExecuteDeleteAsync();

            foreach (var host in _hosts) {
                _db.AcmeHosts.Add(host);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        if (success) _nav.NavigateTo("/acme");
    }

    public void Dispose() {
        _db?.Dispose();
    }
}
