namespace ByProxy.Services {
    public class AcmeClientService {
        private readonly ILogger<AcmeClientService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ScriptCompilationService _scriptCompilation;

        private readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly NonceCache<AcmeProvider, string> _nonceCache = new();
        private readonly ExpiringObjectCache<AcmeProvider, AcmeDirectory> _directoryCache = new();
        private readonly Dictionary<AcmeProvider, DateTime> _rateLimitedProviders = new();

        public readonly ConcurrentDictionary<string, AcmeHttp01Challenge> PendingHttp01Challenges = new();

        public ImmutableList<AcmeProvider> Providers { get; init; }

        public AcmeClientService(
            ILogger<AcmeClientService> logger,
            IHttpClientFactory httpClientFactory,
            ScriptCompilationService scriptCompilation,
            IConfiguration config
        ) {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _scriptCompilation = scriptCompilation;

            var providers = new List<AcmeProvider>();
            var providerConfigs = config.GetSection("Acme:Providers").GetChildren().ToArray();
            for (int i = 0; i < providerConfigs.Length; i++) {
                var id = providerConfigs[i]["Id"] ?? throw new Exception("Provider Id missing.");
                var name = providerConfigs[i]["Name"] ?? id;
                var directory = providerConfigs[i]["DirectoryUrl"] ?? throw new Exception("DirectoryUrl missing.");
                var staging = providerConfigs[i]["StagingUrl"];
                var challenges = providerConfigs[i].GetSection("Challenges").Get<string[]>();
                if (challenges == null || challenges.Length == 0) throw new Exception("No challenges defined.");
                var contactEmailsOptional = providerConfigs[i].GetValue<bool>("ContactEmailsOptional", false);
                
                challenges = challenges.Where(challengeType => challengeType is AcmeChallengeType.HTTP_01 or AcmeChallengeType.DNS_01).ToArray();

                providers.Add(new AcmeProvider(id, name, directory, staging, challenges, contactEmailsOptional));
            }
            Providers = providers.ToImmutableList();
        }

        private string GetKeyPath(Guid accountId) => Path.Combine(AppPaths.AcmeAccountDir, $"{accountId}.key");

        public AcmeProvider GetProvider(string providerId) => Providers.First(_ => _.Id == providerId);

        private ECDsa ImportAccountKeyFromDisk(Guid accountId) {
            var filePath = GetKeyPath(accountId);
            var key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(File.ReadAllBytes(filePath), out _);
            return key;
        }

        private void ExportAccountKeyToDisk(Guid accountId, ECDsa key) {
            var filePath = GetKeyPath(accountId);
            byte[] keyBytes = key.ExportPkcs8PrivateKey();
            File.WriteAllBytes(filePath, keyBytes);
        }

        private string ExtractNonceFromResponse(HttpResponseMessage response) {
            if (!response.Headers.TryGetValues("Replay-Nonce", out var nonceValue)) throw new Exception("Replay-Nonce header is missing.");

            var nonce = nonceValue.First();
            if (string.IsNullOrWhiteSpace(nonce)) throw new Exception("Replay-Nonce was empty.");

            return nonce;
        }

        private TimeSpan ExtractRetryAfterDelay(HttpResponseMessage response) {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter == null) throw new Exception("Retry-After header is missing.");

            if (retryAfter.Delta.HasValue) {
                if (retryAfter.Delta.Value < TimeSpan.Zero) throw new Exception("Retry-After delta was negative.");
                return retryAfter.Delta.Value;
            }

            if (retryAfter.Date.HasValue) {
                var delay = retryAfter.Date.Value.Subtract(DateTimeOffset.UtcNow);
                return delay <= TimeSpan.Zero ? TimeSpan.Zero : delay;
            }

            throw new Exception("Unable to parse Retry-After");
        }

        private bool TryExtractNonceFromResponse(HttpResponseMessage response, out string nonce) {
            try {
                nonce = ExtractNonceFromResponse(response);
                return true;
            } catch { }
            nonce = default!;
            return false;
        }

        private bool TryExtractRetryAfterDelay(HttpResponseMessage response, out TimeSpan delay) {
            try {
                delay = ExtractRetryAfterDelay(response);
                return true;
            } catch {
                delay = default;
                return false;
            }
        }

        public async Task<AcmeDirectory> GetDirectoryAsync(AcmeProvider provider, CancellationToken cancellationToken = default) {
            var directory = _directoryCache.GetValueOrDefault(provider);

            if (directory == null) {
                var http = _httpClientFactory.CreateClient(HttpClientNames.AcmeClient);

                directory = await http.GetFromJsonAsync<AcmeDirectory>(provider.DirectoryUrl, JsonOptions, cancellationToken);
                if (directory == null || string.IsNullOrWhiteSpace(directory.NewNonce) || string.IsNullOrWhiteSpace(directory.NewAccount) || string.IsNullOrWhiteSpace(directory.NewOrder)) {
                    throw new Exception("Invalid ACME directory payload.");
                }

                _directoryCache.AddOrUpdate(provider, directory, TimeSpan.FromHours(6.5));
            }
            return directory;
        }

        private async Task<string> GetNewNonceAsync(AcmeDirectory directory, CancellationToken cancellationToken = default) {
            var http = _httpClientFactory.CreateClient(HttpClientNames.AcmeClient);

            using var request = new HttpRequestMessage(HttpMethod.Head, directory.NewNonce);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            if (!response.Headers.TryGetValues("Replay-Nonce", out var nonceValue))
                throw new Exception("Replay-Nonce header is missing.");

            var nonce = nonceValue.First();
            if (string.IsNullOrWhiteSpace(nonce)) throw new Exception("Replay-Nonce was empty.");

            return nonce;
        }

        private async Task<string> GetNonceAsync(AcmeProvider provider, CancellationToken cancellationToken = default) {
            if (_rateLimitedProviders.TryGetValue(provider, out var rateLimitExpiry)) {
                if (rateLimitExpiry > DateTime.UtcNow) throw new Exception($"Acme Provider {provider.Name} is rate limited until {rateLimitExpiry}");
                _rateLimitedProviders.Remove(provider);
            }

            var nonce = _nonceCache.GetValueOrDefault(provider);
            if (nonce != null) return nonce;

            var directory = await GetDirectoryAsync(provider, cancellationToken);
            return await GetNewNonceAsync(directory, cancellationToken);
        }

        private async Task AssertAcmeSuccessResponseAsync(AcmeProvider provider, HttpResponseMessage response, CancellationToken cancellationToken) {
            if (TryExtractNonceFromResponse(response, out var nonce)) {
                _nonceCache.Add(provider, nonce);
            }

            if (response.Content.Headers.ContentType?.MediaType == "application/problem+json") {
                var problem = await response.Content.ReadFromJsonAsync<AcmeProblem>(cancellationToken);
                if (problem.IsRateLimit) {
                    if (TryExtractRetryAfterDelay(response, out var retryDelay)) {
                        _rateLimitedProviders[provider] = DateTime.UtcNow.Add(retryDelay);
                    } else {
                        _rateLimitedProviders[provider] = DateTime.UtcNow.AddHours(1);
                    }
                }
                throw new AcmeProblemException(response, problem);
            }

            if (!response.IsSuccessStatusCode) {
                await using var bodyStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var bodyBuffer = new byte[8192];
                var bytesRead = bodyStream.ReadAtLeast(bodyBuffer, bodyBuffer.Length, false);
                var body = Encoding.UTF8.GetString(bodyBuffer, 0, bytesRead);
                throw new HttpRequestException(
                    $"{(int)response.StatusCode} {response.ReasonPhrase} on {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}:\n{body}",
                    null,
                    response.StatusCode
                );
            }
        }

        public async Task<string> CreateNewAccountAsync(Guid newAccountId, AcmeProvider provider, IEnumerable<string>? contactEmails, CancellationToken cancellationToken = default) {
            using var newAccountKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var directory = await GetDirectoryAsync(provider, cancellationToken);
            using var response = await AcmePostAsync(provider, null, newAccountKey, directory.NewAccount, new AcmeNewAccountRequest(contactEmails), cancellationToken);

            var accountUrl = response.Headers.Location?.ToString();
            if (string.IsNullOrWhiteSpace(accountUrl)) throw new Exception("Missing Location header / account URL.");
                        
            ExportAccountKeyToDisk(newAccountId, newAccountKey);
            
            return accountUrl;
        }

        public void PurgeAccount(Guid accountId) {
            var filePath = GetKeyPath(accountId);
            File.Delete(filePath);
        }

        public async Task<X509Certificate2Collection> RequestNewCertificateAsync(AcmeAccount account, IEnumerable<AcmeCertHost> hosts, IProgress<(string Step, string? Detail)>? progress = null, CancellationToken cancellationToken = default) {
            progress?.Report(("AcmeStep_LoadAccountKey", account.Id.ToString()));
            using var accountKey = ImportAccountKeyFromDisk(account.Id);
            
            var hostNames = hosts.Select(_ => _.Host);
            progress?.Report(("AcmeStep_SubmitOrder", string.Join(", ", hostNames)));

            var orderResponse = await CreateNewOrderAsync(account, accountKey, hostNames, cancellationToken);

            using var deadlineTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineTokenSource.Token);
            var certificate = await ProcessOrderAsync(account, accountKey, hosts, orderResponse.OrderUrl, orderResponse.Order, progress, combinedTokenSource.Token);

            progress?.Report(("AcmeStep_OrderComplete", null));
            return certificate;
        }

        private async Task<X509Certificate2Collection> ProcessOrderAsync(AcmeAccount account, ECDsa accountKey, IEnumerable<AcmeCertHost> hosts, string orderUrl, AcmeOrder order, IProgress<(string Step, string? Detail)>? progress = null, CancellationToken cancellationToken = default) {
            _logger.LogInformation($"Processing ACME Order for {account.Name}: {orderUrl}");
            progress?.Report(("AcmeStep_ProcessingOrder", orderUrl));
            ECDsa? privateKey = null;
            while (!cancellationToken.IsCancellationRequested) {
                switch (order.Status) {
                    case "pending":
                        _logger.LogInformation("Order is pending, challenges are required.");
                        progress?.Report(("AcmeStep_OrderPending", null));
                        foreach (var authUrl in order.Authorizations) {
                            await ProcessOrderAuthorizationAsync(account, accountKey, hosts, authUrl, progress, cancellationToken);
                        }
                        _logger.LogInformation("Waiting for order update.");
                        progress?.Report(("AcmeStep_WaitingOrderUpdate", null));
                        while (!cancellationToken.IsCancellationRequested) {
                            order = await AcmePostAsGetAsync<AcmeOrder>(account, accountKey, orderUrl, cancellationToken);
                            if (order.Status != "pending") break;
                            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                        }
                        if (order.Status == "pending") throw new Exception("ACME order timed out while pending.");
                        break;
                    case "ready":
                        progress?.Report(("AcmeStep_GeneratingCSR", null));
                        (privateKey, var csr) = AcmeHelpers.GenerateCsr(order.Identifiers);
                        var payload = new { csr = AcmeHelpers.Base64UrlEncode(csr) };

                        _logger.LogInformation($"Sending CSR to finalize order {orderUrl}: {order.Finalize}");
                        progress?.Report(("AcmeStep_SubmittingCSR", order.Finalize));
                        order = await AcmePostAsync<AcmeOrder>(account, accountKey, order.Finalize, payload, cancellationToken);
                        break;
                    case "processing":
                        _logger.LogInformation("Waiting for order completion.");
                        progress?.Report(("AcmeStep_WaitingOrderCompletion", null));
                        while (!cancellationToken.IsCancellationRequested) {
                            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                            order = await AcmePostAsGetAsync<AcmeOrder>(account, accountKey, orderUrl, cancellationToken);
                            if (order.Status != "processing") break;
                        }
                        if (order.Status == "processing") throw new Exception("ACME order timed out while processing.");
                        break;
                    case "valid":
                        if (order.Certificate == null) throw new Exception("ACME order in 'valid' state, but certificate URL not present.");
                        if (privateKey == null) throw new Exception("ACME order in 'valid' state, but certificate private key missing.");
                        
                        _logger.LogInformation($"Downloading certificate for order {orderUrl}.");
                        progress?.Report(("AcmeStep_DownloadingCertificate", order.Certificate));
                        var acmeCert = await AcmePostAsGetAsync<string>(account, accountKey, order.Certificate, cancellationToken);
                        return Certificates.ImportPemChainAndKey(acmeCert, privateKey);
                    default:
                        throw new Exception($"ACME order in invalid state: {order.Status}");
                }
            }
            throw new Exception("Order processing timed out.");
        }

        private async Task ProcessOrderAuthorizationAsync(AcmeAccount account, ECDsa accountKey, IEnumerable<AcmeCertHost> hosts, string authUrl, IProgress<(string Step, string? Detail)>? progress = null, CancellationToken cancellationToken = default) {
            progress?.Report(("AcmeStep_ProcessingAuthorization", authUrl));
            var authorization = await AcmePostAsGetAsync<AcmeAuthorization>(account, accountKey, authUrl, cancellationToken);
            
            progress?.Report(("AcmeStep_AuthorizationStatus", authorization.Status));
            if (authorization.Status == "valid") return;
            if (authorization.Status != "pending") throw new Exception($"Acme Order Authorization for {authorization.Identifier.Value} is {authorization.Status}");

            AcmeCertHost host;
            if (authorization.Wildcard ?? false) {
                host = hosts.First(_ => _.Host == $"*.{authorization.Identifier.Value}");
            } else {
                host = hosts.First(_ => _.Host == authorization.Identifier.Value);
            }
            var challenge = authorization.Challenges.First(_ => string.Equals(_.Type, host.ChallengeType, StringComparison.OrdinalIgnoreCase));
            progress?.Report(("AcmeStep_AuthorizationChallengeHost", $"{host.Host} ({host.ChallengeType})"));

            Func<IProgress<(string, string?)>?, Task>? cleanupTask = null;
            if (host is AcmeDnsCertHost startDnsChallenge) {
                _logger.LogInformation($"Performing ACME DNS-01 Challenge for {host.Host} ({authUrl})");
                cleanupTask = await PerformDns01ChallengeAsync(account, accountKey, challenge, startDnsChallenge, progress, cancellationToken);
            } else if (host.ChallengeType == AcmeChallengeType.HTTP_01) {
                _logger.LogInformation($"Performing ACME HTTP-01 Challenge for {host.Host} ({authUrl})");
                await PerformHttp01ChallengeAsync(account, accountKey, challenge, progress, cancellationToken);
            } else {
                throw new NotImplementedException();
            }

            try {
                _logger.LogInformation("Waiting for authorization update.");
                while (!cancellationToken.IsCancellationRequested) {
                    progress?.Report(("AcmeStep_WaitingAuthorizationUpdate", null));
                    authorization = await AcmePostAsGetAsync<AcmeAuthorization>(account, accountKey, authUrl, cancellationToken);
                    if (authorization.Status != "pending") break;
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
                if (authorization.Status == "valid") {
                    _logger.LogInformation("Authorization now valid.");
                    progress?.Report(("AcmeStep_AuthorizationComplete", null));
                    return;
                }
            } finally {
                if (cleanupTask != null) await cleanupTask.Invoke(progress);
            }
            throw new Exception($"Acme Order Authorization for {authorization.Identifier.Value} is {authorization.Status}: {JsonSerializer.Serialize(authorization)}");
        }

        private async Task<Func<IProgress<(string, string?)>?, Task>?> PerformDns01ChallengeAsync(AcmeAccount account, ECDsa accountKey, AcmeChallenge challenge, AcmeDnsCertHost dnsHost, IProgress<(string Step, string? Detail)>? progress = null, CancellationToken cancellationToken = default) {
            progress?.Report(("AcmeStep_StartingDns01", challenge.Url));

            var keyAuthorization = $"{challenge.Token}.{AcmeHelpers.GenerateJwkThumbprint(accountKey)}";
            var txtValue = AcmeHelpers.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(keyAuthorization)));
            var provider = await _scriptCompilation.RetrieveAcmeDnsProvider(dnsHost.DnsProviderId);
            var dnsDomain = dnsHost.Host.StartsWith("*.") ? dnsHost.Host.Substring(2) : dnsHost.Host;

            var createSuccess = await provider.CreateDnsRecord(dnsDomain, txtValue);
            if (!createSuccess) throw new Exception("CreateDnsRecord returned false indicating the script was unsuccessful at creating the required DNS record.");
            _logger.LogInformation($"DNS Record Created: _acme-challenge.{dnsDomain}.  TXT  \"{txtValue}\"");

            async Task cleanupTask(IProgress<(string, string?)>? progress) {
                var deleteSuccess = await provider.DeleteDnsRecord(dnsDomain, txtValue);
                if (deleteSuccess) {
                    _logger.LogInformation("Cleaned up TXT record from DNS-01 Challenge.");
                    progress?.Report(("AcmeStep_Dns01RecordDeleted", "Removal was successful."));
                } else {
                    _logger.LogWarning("Failed to cleanup TXT record from DNS-01 Challenge.");
                    progress?.Report(("AcmeStep_Dns01RecordDeleted", "Removal possibly failed."));
                }
            }

            try {
                progress?.Report(("AcmeStep_Dns01RecordCreated", $"_acme-challenge.{dnsDomain}.  TXT  \"{txtValue}\""));
                progress?.Report(("AcmeStep_SubmittingDns01", challenge.Token));
                _ = await AcmePostAsync<AcmeChallenge>(account, accountKey, challenge.Url, new object { }, cancellationToken);
            } catch {
                await cleanupTask(progress);
                throw;
            }
            return cleanupTask;
        }

        private async Task PerformHttp01ChallengeAsync(AcmeAccount account, ECDsa accountKey, AcmeChallenge challenge, IProgress<(string Step, string? Detail)>? progress = null, CancellationToken cancellationToken = default) {
            progress?.Report(("AcmeStep_StartingHttp01", challenge.Url));

            var keyAuthorization = $"{challenge.Token}.{AcmeHelpers.GenerateJwkThumbprint(accountKey)}";

            var pendingHttp01 = new AcmeHttp01Challenge(Encoding.ASCII.GetBytes(keyAuthorization));
            PendingHttp01Challenges.AddOrUpdate(challenge.Token, pendingHttp01, (_, existing) => {
                existing.TaskCompletionSource.TrySetCanceled();
                return pendingHttp01;
            });

            progress?.Report(("AcmeStep_SubmittingHttp01", challenge.Token));
            _ = await AcmePostAsync<AcmeChallenge>(account, accountKey, challenge.Url, new object { }, cancellationToken);

            _logger.LogInformation("Waiting for ACME server to request HTTP-01 challenge...");
            progress?.Report(("AcmeStep_WaitingHttp01", null));
            await pendingHttp01.TaskCompletionSource.Task.WaitAsync(TimeSpan.FromMinutes(2), cancellationToken);

            _logger.LogInformation("Responded to HTTP-01 challenge.");
            progress?.Report(("AcmeStep_Http01Responded", null));
        }

        private async Task<AcmeOrderResponse> CreateNewOrderAsync(AcmeAccount account, ECDsa accountKey, IEnumerable<string> dnsNames, CancellationToken cancellationToken = default) {
            var provider = GetProvider(account.Provider);
            var directory = await GetDirectoryAsync(provider, cancellationToken);

            var orderRequest = AcmeNewOrderRequest.CreateDnsOrder(dnsNames);

            _logger.LogInformation($"Submitting new ACME order for account {account.Name}: {string.Join(", ", dnsNames)}");
            using var response = await AcmePostAsync(account, accountKey, directory.NewOrder, orderRequest, cancellationToken);

            var orderUrl = response.Headers.Location?.ToString();
            if (string.IsNullOrWhiteSpace(orderUrl)) throw new Exception("Missing Location header / order URL.");

            var order = await response.Content.ReadFromJsonAsync<AcmeOrder>(JsonOptions, cancellationToken);
            if (order == null) throw new Exception("Unable to parse order response.");

            return new AcmeOrderResponse(orderUrl, order);
        }

        public async Task<string> TestPostAsGetAsync(AcmeAccount account, string url) {
            using var accountKey = ImportAccountKeyFromDisk(account.Id);
            return await AcmePostAsGetAsync<string>(account, accountKey, url);
        }

        private async Task<T> AcmePostAsync<T>(AcmeAccount account, ECDsa accountKey, string url, object? payload, CancellationToken cancellationToken = default) {
            using var response = await AcmePostAsync(account, accountKey, url, payload, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            if (result == null) throw new Exception("Unable to parse response.");
            return result;
        }

        private Task<HttpResponseMessage> AcmePostAsync(AcmeAccount account, ECDsa accountKey, string url, object? payload, CancellationToken cancellationToken = default) {
            var provider = GetProvider(account.Provider);
            return AcmePostAsync(provider, account.Url, accountKey, url, payload, cancellationToken);
        }

        private async Task<T> AcmePostAsGetAsync<T>(AcmeAccount account, ECDsa accountKey, string url, CancellationToken cancellationToken = default) {
            using var response = await AcmePostAsync(account, accountKey, url, null, cancellationToken);
            if (typeof(T) == typeof(string)) {
                return (T)(object)await response.Content.ReadAsStringAsync(cancellationToken);
            }
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            if (result == null) throw new Exception("Unable to parse response.");
            return result;
        }

        private async Task<HttpResponseMessage> AcmePostAsync(AcmeProvider provider, string? accountUrl, ECDsa accountKey, string url, object? payload, CancellationToken cancellationToken = default) {
            var nonce = await GetNonceAsync(provider);

            using var request = accountUrl == null ?
                AcmeHelpers.GenerateAcmeMessage(accountKey, nonce, url, payload) //Jwk
                : AcmeHelpers.GenerateAcmeMessage(accountUrl, accountKey, nonce, url, payload); //Kid

            var http = _httpClientFactory.CreateClient(HttpClientNames.AcmeClient);
            var response = await http.PostAsync(url, request, cancellationToken);
            try {
                await AssertAcmeSuccessResponseAsync(provider, response, cancellationToken);
                return response;
            } catch (AcmeProblemException ex) {
                if (!ex.AcmeProblem.IsBadNonce) throw;
                response.Dispose();

                var newNonce = await GetNonceAsync(provider);
                using var newRequest = accountUrl == null ?
                    AcmeHelpers.GenerateAcmeMessage(accountKey, nonce, url, payload) //Jwk
                    : AcmeHelpers.GenerateAcmeMessage(accountUrl, accountKey, nonce, url, payload); //Kid

                response = await http.PostAsync(url, request, cancellationToken);
                await AssertAcmeSuccessResponseAsync(provider, response, cancellationToken);
                return response;
            }
        }
    }
}
