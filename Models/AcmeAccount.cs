namespace ByProxy.Models {
    public class AcmeAccount {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }
        public string Url { get; set; }
        public bool Hidden { get; set; }

        public async Task<AcmeAccountExport> ToExportAsync(CancellationToken cancellationToken = default) {
            byte[] bytes = await File.ReadAllBytesAsync(AcmeClientService.GetKeyFilePath(Id), cancellationToken);
            string key = Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);
            return new AcmeAccountExport {
                Id = Id,
                Name = Name,
                Provider = Provider,
                Url = Url,
                Hidden = Hidden,
                Key = key
            };
        }
    }

    public class AcmeAccountExport {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }
        public string Url { get; set; }
        public bool Hidden { get; set; }
        public string Key { get; set; }
    }
}
