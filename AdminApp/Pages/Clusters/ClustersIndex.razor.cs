namespace ByProxy.AdminApp.Pages.Clusters {
    public partial class ClustersIndex {
        private ProxyDb _db = default!;
        private List<ProxyCluster>? _clusters;

        protected override async Task OnInitializedAsync() {
            _db = _dbFactory.CreateDbContext();
            await ReloadClusters();
        }

        private async Task ReloadClusters() {
            _db.ChangeTracker.Clear();

            _clusters = await _db.Clusters
                .Include(c => c.Destinations.OrderBy(d => d.Order))
                .ToListAsync();
            _clusters.ApplySort();
        }

        private void EditCluster(ProxyCluster cluster) {
            _nav.NavigateTo($"/clusters/{cluster.Id}");
        }

        private async Task CloneCluster(ProxyCluster cluster) {
            var result = await _modal.SingleValue(_strings["Clone Cluster"], _strings["CloneClusterPrompt"], cluster.Name);
            if (result.DialogResult != DialogResult.OK) return;

            var newCluster = cluster.Clone(cluster.ConfigRevision);
            newCluster.Id = Guid.NewGuid();
            newCluster.Name = result.Value;
            foreach (var destination in newCluster.Destinations) {
                destination.Id = Guid.NewGuid();
                destination.ClusterId = newCluster.Id;
            }

            var success = await _modal.PerformSave(async () => {
                _db.Clusters.Add(newCluster);
                await _db.SaveChangesAsync();
            });

            if (success) {
                _ = _config.UpdateHasChangesPending();
                await ReloadClusters();
            }
        }

        private async Task DeleteCluster(ProxyCluster cluster) {
            var result = await _modal.YesNo(_strings["Delete Cluster"], $"{_strings["Delete"]}: {cluster.Name}\n\n{_strings["WARN_Delete"]}");
            if (result != DialogResult.Yes) return;

            var success = await _modal.PerformProcessing(async () => {
                var routes = await _db.Routes
                    .AsNoTracking()
                    .Where(_ => _.ClusterId == cluster.Id)
                    .Select(_ => _.Name)
                    .ToListAsync();

                if (routes.Count > 0) {
                    throw new Exception($"{_strings["ERR_DeleteFailed"]}: {cluster.Name}\n\n{_strings["ERR_RouteConstraint"]}:\n- {string.Join("\n- ", routes)}");
                }

                _db.Clusters.Remove(cluster);
                await _db.SaveChangesAsync();
            });

            if (success) {
                _ = _config.UpdateHasChangesPending();
                await ReloadClusters();
            }
        }

        public void Dispose() {
            _db?.Dispose();
        }
    }
}
