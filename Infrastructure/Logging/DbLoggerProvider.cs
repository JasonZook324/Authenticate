using Authenticate.Data;
using Authenticate.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Authenticate.Infrastructure.Logging
{
    public sealed class DbLoggerProvider : ILoggerProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly LogLevel _minLevel;

        public DbLoggerProvider(IServiceScopeFactory scopeFactory, LogLevel minLevel = LogLevel.Information)
        {
            _scopeFactory = scopeFactory;
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName) => new DbLogger(categoryName, _scopeFactory, _minLevel);

        public void Dispose() { }

        private sealed class DbLogger : ILogger
        {
            private readonly string _category;
            private readonly IServiceScopeFactory _scopeFactory;
            private readonly LogLevel _minLevel;

            // Disable on first failure to avoid flooding when table is missing or DB is down
            private static volatile bool _disabled;
            private static readonly bool _disabledByEnv =
                string.Equals(Environment.GetEnvironmentVariable("DISABLE_DB_LOGGER"), "1", StringComparison.OrdinalIgnoreCase);

            // Prevent recursive logging (e.g., logging errors caused by logging)
            [ThreadStatic] private static bool _isLogging;

            private static readonly string[] IgnoredPrefixes =
            {
                "Microsoft.EntityFrameworkCore",          // avoid logging EF (esp. migrations/command logs)
                "Microsoft.AspNetCore.Diagnostics",       // exception page diagnostics
                "Microsoft.AspNetCore.Server.Kestrel",    // noisy connection logs
            };

            public DbLogger(string category, IServiceScopeFactory scopeFactory, LogLevel minLevel)
            {
                _category = category;
                _scopeFactory = scopeFactory;
                _minLevel = minLevel;
            }

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) =>
                !_disabled && !_disabledByEnv && logLevel >= _minLevel && !ShouldIgnoreCategory(_category);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel) || formatter is null) return;
                if (_isLogging) return; // no re-entrancy

                _isLogging = true;
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var accessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
                    var http = accessor?.HttpContext;

                    var entry = new LogEntry
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Category = _category,
                        Level = logLevel.ToString(),
                        EventId = eventId.Id == 0 ? null : eventId.Id,
                        Message = SafeTruncate(formatter(state, exception), 4000),
                        Exception = exception?.ToString(),
                        UserName = http?.User?.Identity?.IsAuthenticated == true ? http.User.Identity?.Name : null,
                        RequestPath = http?.Request?.Path.ToString(),
                        RequestId = http?.TraceIdentifier,
                        RemoteIp = http?.Connection?.RemoteIpAddress?.ToString()
                    };

                    db.Logs.Add(entry);
                    db.SaveChanges();
                }
                catch
                {
                    // Disable for the remainder of the process to avoid repeated failures
                    _disabled = true;
                }
                finally
                {
                    _isLogging = false;
                }
            }

            private static bool ShouldIgnoreCategory(string category)
            {
                foreach (var prefix in IgnoredPrefixes)
                    if (category.StartsWith(prefix, StringComparison.Ordinal)) return true;
                return false;
            }

            private static string? SafeTruncate(string? value, int max) =>
                string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}