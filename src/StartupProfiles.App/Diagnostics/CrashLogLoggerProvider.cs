using Microsoft.Extensions.Logging;

namespace StartupProfiles.App.Diagnostics;

/// <summary>
/// Sends warnings and errors from <see cref="ILogger"/> to <see cref="CrashLog"/>, so host and library logs end up
/// in the same error.log as the crashes.
/// </summary>
internal sealed class CrashLogLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CrashLogLogger(categoryName);

    public void Dispose() { }

    private sealed class CrashLogLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            CrashLog.Write($"{category} ({logLevel})", exception is null ? message : $"{message}{Environment.NewLine}{exception}");
        }
    }
}
