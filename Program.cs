using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Authenticate.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Authenticate.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Authenticate.Infrastructure.Email;
using Microsoft.Extensions.Options;
using Authenticate.Infrastructure.Gemini;
using Authenticate.Infrastructure.ApiDocs;
using Authenticate.Services.Testing;

// Load environment variables from .env file
Env.Load();

// Get connection string from environment
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");

var builder = WebApplication.CreateBuilder(args);

// Register ApplicationDbContext with PostgreSQL provider
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Auth
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
    });

// MVC
builder.Services.AddControllersWithViews();

// HTTP clients (for testing API credentials)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IApiCredentialTester, EspnTester>();
builder.Services.AddSingleton<IApiCredentialTester, GeminiTester>();
builder.Services.AddSingleton<IApiCredentialTester, DefaultTester>();
builder.Services.AddSingleton<ApiCredentialTestService>();
builder.Services.AddScoped<IApiDocIngestionService, OpenApiIngestionService>();

// DB logging provider + filters to avoid EF self-logging loops
builder.Services.AddHttpContextAccessor();

// Determine DB logger minimum level from env (default Warning)
var dbLoggerMinEnv = Environment.GetEnvironmentVariable("DB_LOGGER_MINLEVEL") ?? "Warning";
var dbLoggerMinLevel = dbLoggerMinEnv.ToLowerInvariant() switch
{
    "trace" => LogLevel.Trace,
    "debug" => LogLevel.Debug,
    "information" => LogLevel.Information,
    "warning" => LogLevel.Warning,
    "error" => LogLevel.Error,
    "critical" => LogLevel.Critical,
    _ => LogLevel.Warning
};

// Register DbLoggerProvider with chosen minimum level
builder.Services.AddSingleton<ILoggerProvider>(sp =>
    new DbLoggerProvider(sp.GetRequiredService<IServiceScopeFactory>(), dbLoggerMinLevel));

// Configure logging filters for this provider
builder.Logging.AddFilter<DbLoggerProvider>(null, dbLoggerMinLevel);
builder.Logging.AddFilter<DbLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter<DbLoggerProvider>("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter<DbLoggerProvider>("Microsoft.AspNetCore.Diagnostics", LogLevel.None);

builder.Services.AddSingleton<ILoggerProvider, DbLoggerProvider>();
builder.Logging.AddFilter<DbLoggerProvider>(null, LogLevel.Information);
builder.Logging.AddFilter<DbLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter<DbLoggerProvider>("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter<DbLoggerProvider>("Microsoft.AspNetCore.Diagnostics", LogLevel.None);

// Email options from environment
var smtpOptions = new SmtpOptions
{
    Host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "",
    Port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : 587,
    UseStartTls = (Environment.GetEnvironmentVariable("SMTP_STARTTLS") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase),
    User = Environment.GetEnvironmentVariable("SMTP_USER"),
    Password = Environment.GetEnvironmentVariable("SMTP_PASS"),
    From = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "no-reply@example.com",
    FromName = Environment.GetEnvironmentVariable("SMTP_FROMNAME") ?? "MyApp"
};
builder.Services.AddSingleton<IOptions<SmtpOptions>>(_ => Options.Create(smtpOptions));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// Gemini options from environment
var geminiOptions = new GeminiOptions
{
    BaseUrl = Environment.GetEnvironmentVariable("GEMINI_BASEURL")?.TrimEnd('/') 
              ?? "https://generativelanguage.googleapis.com",
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
};
builder.Services.AddSingleton(geminiOptions);
builder.Services.AddHttpClient<IGeminiMetadataClient, GeminiMetadataClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<NFLTeamSyncService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
