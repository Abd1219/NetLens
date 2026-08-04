using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using NetLens.Application.Abstractions;
using NetLens.Application.Services;
using NetLens.Database;
using NetLens.Domain.Rules;
using NetLens.Infrastructure.Repositories;
using NetLens.Network.Adapters;
using NetLens.Network.Diagnostics;
using NetLens.Network.Wifi;
using NetLens.Network.Discovery;
using NetLens.Network.PacketCapture;
using NetLens.Reporting;
using NetLens.Services;
using NetLens.UI.ViewModels;
using NetLens.UI.Views;

namespace NetLens.UI;

/// <summary>
/// Application entry point and DI host composition root.
/// Uses Microsoft.Extensions.Hosting for full lifecycle management.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    public static IHost Host { get; private set; } = null!;
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>
    /// Shorthand for accessing the DI container.
    /// </summary>
    public static IServiceProvider Services => Host.Services;

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
            })
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // ── Database ────────────────────────────────────────
        services.AddDbContext<NetLensDbContext>(options =>
            options.UseSqlite("Data Source=netlens.db"));

        // ── Infrastructure ──────────────────────────────────
        services.AddScoped<ISessionRepository, SessionRepository>();

        // ── Domain Rules (all registered as IEnumerable<IDiagnosticRule>) ──
        services.AddTransient<IDiagnosticRule, LowRSSIRule>();
        services.AddTransient<IDiagnosticRule, HighPacketLossRule>();
        services.AddTransient<IDiagnosticRule, GatewayLatencyRule>();
        services.AddTransient<IDiagnosticRule, DnsLatencyRule>();
        services.AddTransient<IDiagnosticRule, HighJitterRule>();

        // ── Application ─────────────────────────────────────
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IRuleEngine, RuleEngine>();

        // ── Network ─────────────────────────────────────────
        services.AddSingleton<PingService>();
        services.AddSingleton<SystemMetricsCollector>();
        services.AddSingleton<ITelemetryCollector, WifiTelemetryCollector>();

        // ── Discovery & Capture ──────────────────────────────
        services.AddSingleton<ArpResolver>();
        services.AddSingleton<HostnameResolver>();
        services.AddSingleton<SubnetScanner>();
        services.AddSingleton<TracerouteService>();
        services.AddSingleton<IPacketCapture, NullPacketCapture>();

        // ── Background Services ──────────────────────────────
        services.AddHostedService<TelemetryBackgroundService>();
        services.AddHostedService<CorrelationEngine>();

        // ── Reporting ────────────────────────────────────────
        services.AddSingleton<IReportGenerator, DiagnosticReportGenerator>();

        // ── UI — Windows & Pages ─────────────────────────────
        services.AddTransient<MainWindow>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<WifiExplorerPage>();
        services.AddTransient<DiscoveryPage>();
        services.AddTransient<DiagnosticsPage>();
        services.AddTransient<Views.SettingsPage>();
        services.AddTransient<HistoryPage>();

        // Settings & Localization
        services.AddSingleton<Services.SettingsService>();
        services.AddSingleton<Services.LocalizationService>();

        // ── UI — ViewModels ──────────────────────────────────
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<WifiExplorerViewModel>();
        services.AddSingleton<DiscoveryViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<HistoryViewModel>();
    }

    /// <summary>
    /// SQLite schema version. Bump when persistence entities change (e.g. DateTimeOffset -> DateTime).
    /// </summary>
    private const int DatabaseSchemaVersion = 2;

    private static async Task EnsureDatabaseSchemaAsync(NetLensDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var versionCommand = db.Database.GetDbConnection().CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version";
            var storedVersion = Convert.ToInt32(await versionCommand.ExecuteScalarAsync() ?? 0);

            if (storedVersion == DatabaseSchemaVersion)
                return;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync($"PRAGMA user_version = {DatabaseSchemaVersion}");
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await Host.StartAsync();

        // Initialize localization from persisted settings (if any)
        try
        {
            var settings = Services.GetRequiredService<Services.SettingsService>();
            var loc = Services.GetRequiredService<Services.LocalizationService>();
            var lang = settings.GetLanguage();
            if (!string.IsNullOrWhiteSpace(lang))
            {
                loc.SetLanguage(lang);
            }
        }
        catch
        {
            // ignore if services not available
        }

        // Ensure DB schema is current (recreates when version changes)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NetLensDbContext>();
        await EnsureDatabaseSchemaAsync(db);

        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();
    }
}
