namespace ByProxy.Infrastructure {
    public class LogFormatter : ConsoleFormatter, IDisposable {
        private readonly IDisposable? _optionsReloadToken;
        private LogFormatterOptions _formatterOptions;

        public LogFormatter(IOptionsMonitor<LogFormatterOptions> options) : base("ByProxy") {
            (_optionsReloadToken, _formatterOptions) = (options.OnChange(ReloadLoggerOptions), options.CurrentValue);
        }

        private void ReloadLoggerOptions(LogFormatterOptions options) => _formatterOptions = options;

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter) {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (message == null) return;

            textWriter.Write(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss "));

            if (logEntry.LogLevel != LogLevel.Information) {
                switch (logEntry.LogLevel) {
                    case LogLevel.Trace: textWriter.Write("TRACE: "); break;
                    case LogLevel.Debug: textWriter.Write("DEBUG: "); break;
                    case LogLevel.Warning: textWriter.Write("WARN: "); break;
                    case LogLevel.Error: textWriter.Write("ERROR: "); break;
                    case LogLevel.Critical: textWriter.Write("CRIT: "); break;
                }
            }

            if (_formatterOptions.IncludeCategory) textWriter.Write($"[{logEntry.Category}] ");

            textWriter.WriteLine(message);
        }

        public void Dispose() => _optionsReloadToken?.Dispose();
    }

    public class LogFormatterOptions : ConsoleFormatterOptions {
        public bool IncludeCategory { get; set; } = false;
    }

    public static class LogFormatterExtensions {
        public static ILoggingBuilder AddLogFormatter(this ILoggingBuilder builder) {
            return builder.AddConsole(options => options.FormatterName = "ByProxy")
                .AddConsoleFormatter<LogFormatter, LogFormatterOptions>();
        }
    }
}
