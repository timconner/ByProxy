namespace ByProxy.AdminApp.Components {
    public class LocalizingProgress : IProgress<(string Main, string? Detail)> {
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
