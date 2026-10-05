using Avis.App.Simulation;
using Avis.App.Ui;
using Avis.App.Workers;
using Avis.Camera;
using Avis.Configuration;
using Avis.Faults;
using Avis.IFactory;
using Avis.Imaging;
using Avis.LightGuide;
using Avis.Scanner;
using Avis.Scanner.Hid;
using Avis.Station;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Avis.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Relative paths (logs, faults, images, INI cache) must resolve next to the
        // exe, not wherever Task Scheduler / a shortcut happened to start us.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "startup-.log"), rollingInterval: RollingInterval.Day)
            .CreateBootstrapLogger();

        // Two copies would mean two badge readers and duplicate Start WIP calls.
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\AVIS-Station", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("AVIS is already running.", "AVIS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error(e.Exception, "Unhandled exception on the UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception - AVIS is terminating");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        IHost? host = null;
        try
        {
            EnvFile.Load();
            host = BuildHost(args);
        }
        catch (ConfigurationValidationException ex)
        {
            Log.Fatal(ex, "AVIS configuration is invalid");
            MessageBox.Show(ex.Message, "AVIS - configuration error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log.CloseAndFlush();
            return 1;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "AVIS failed to start");
            MessageBox.Show($"AVIS failed to start:\n\n{ex.Message}\n\nSee logs\\startup-*.log.", "AVIS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log.CloseAndFlush();
            return 1;
        }

        try
        {
            var form = host.Services.GetRequiredService<Main>();

            // Start the workers only once the window exists (so status updates and
            // operator popups have somewhere to go), and on the thread pool: started
            // from the UI thread they'd capture its SynchronizationContext and run
            // every worker loop continuation on the UI thread.
            form.Shown += (_, _) => Task.Run(async () =>
            {
                try
                {
                    await host.StartAsync();
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "AVIS background services failed to start");
                }
            });

            Application.Run(form);
            return 0;
        }
        finally
        {
            try
            {
                // Off the UI thread - see the StartAsync comment above.
                Task.Run(() => host.StopAsync(TimeSpan.FromSeconds(10))).Wait(TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while stopping AVIS background services");
            }
            host.Dispose();
            Log.CloseAndFlush();
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services));

        T Bind<T>(string section) where T : new()
        {
            var options = new T();
            builder.Configuration.GetSection(section).Bind(options);
            return options;
        }

        var avis = Bind<AvisOptions>(AvisOptions.SectionName);
        var station = Bind<StationOptions>(StationOptions.SectionName);
        var scanner = Bind<ScannerOptions>(ScannerOptions.SectionName);
        var ifactory = Bind<IFactoryOptions>(IFactoryOptions.SectionName);
        var camera = Bind<JabilEyeCameraOptions>(JabilEyeCameraOptions.SectionName);
        var lightGuide = Bind<LightGuideOptions>(LightGuideOptions.SectionName);
        var sequence = Bind<SequenceOptions>(SequenceOptions.SectionName);
        var imageStaging = Bind<ImageStagingOptions>(ImageStagingOptions.SectionName);
        var faultLogOptions = Bind<FaultLogOptions>(FaultLogOptions.SectionName);
        var simulation = Bind<SimulationOptions>(SimulationOptions.SectionName);

        ifactory.Username = Environment.GetEnvironmentVariable("IFACTORY_USERNAME") ?? "";
        ifactory.Password = Environment.GetEnvironmentVariable("IFACTORY_PASSWORD") ?? "";

        // Station INI on the share (falls back to the last good local copy - never throws).
        var ini = AviProjectConfig.Load(avis.IniPath, AvisOptions.ResolvePath(avis.IniCachePath));
        foreach (var warning in ini.Warnings)
        {
            Log.Warning("Station INI: {Warning}", warning);
        }

        var isStg = ini.Stg ?? avis.DefaultEnvironment.Equals("STG", StringComparison.OrdinalIgnoreCase);
        var environmentName = isStg ? "STG" : "PRD";
        var environment = isStg ? avis.Stg : avis.Prd;
        if (!string.IsNullOrWhiteSpace(environment.ApiBaseUrl))
        {
            ifactory.BaseUrl = environment.ApiBaseUrl;
        }

        // [JABIL_EYE] maps this station PC's hostname to its camera's IP.
        var cameraIp = ini.FindJabilEyeIp(Environment.MachineName);
        if (!string.IsNullOrWhiteSpace(cameraIp))
        {
            camera.HostName = cameraIp;
        }

        AppOptionsValidator.Validate(ifactory, scanner, camera, lightGuide, requireIFactory: !simulation.Enabled);
        if (simulation.Enabled)
        {
            Log.Warning("AVIS is running in SIMULATION mode - no scanner, camera, LightGuide or iFactory traffic is real");
        }
        Log.Information("AVIS starting for station {Station} ({Environment}, iFactory {BaseUrl}, camera {Camera}, INI from {IniSource})",
            station.Name, environmentName, ifactory.BaseUrl, camera.HostName, ini.Source);

        builder.Services.AddSingleton(avis);
        builder.Services.AddSingleton(station);
        builder.Services.AddSingleton(scanner);
        builder.Services.AddSingleton(ifactory);
        builder.Services.AddSingleton(camera);
        builder.Services.AddSingleton(lightGuide);
        builder.Services.AddSingleton(sequence);
        builder.Services.AddSingleton(imageStaging);
        builder.Services.AddSingleton(faultLogOptions);
        builder.Services.AddSingleton(ini);
        builder.Services.AddSingleton(new EnvironmentSelection(simulation.Enabled ? "SIMULATION" : environmentName, environment));
        builder.Services.AddSingleton(simulation);
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddSingleton(sp =>
        {
            var faults = new FaultLog(faultLogOptions, station.Name, sp.GetRequiredService<ILogger<FaultLog>>(), TimeProvider.System);
            if (ini.Source != AviProjectConfigSource.Primary)
            {
                faults.Record(FaultCode.ConfigIniUnavailable, FaultSeverity.Warning, string.Join(" ", ini.Warnings.Take(1)));
            }
            return faults;
        });
        builder.Services.AddSingleton(sp => new StationStatus(new StationState
        {
            Environment = environmentName,
            StationName = station.Name,
            ResourceName = camera.ResourceName,
            LightGuide = lightGuide.Enabled ? ConnectionState.Unknown : ConnectionState.Ok,
            Camera = simulation.Enabled ? ConnectionState.Ok : ConnectionState.Unknown,
        }));
        builder.Services.AddSingleton(sp => new CounterStore(
            AvisOptions.ResolvePath(avis.DataDirectory), TimeProvider.System, sp.GetRequiredService<ILogger<CounterStore>>()));
        builder.Services.AddSingleton<CurrentOperatorState>();
        builder.Services.AddSingleton<OperatorAlertService>();
        builder.Services.AddSingleton<IOperatorAlerts>(sp => sp.GetRequiredService<OperatorAlertService>());
        builder.Services.AddSingleton<ImageStager>();
        builder.Services.AddSingleton(sp => new StationSequencer(
            sp.GetRequiredService<IMesClient>(),
            sp.GetRequiredService<CurrentOperatorState>(),
            sp.GetRequiredService<IOperatorAlerts>(),
            sp.GetRequiredService<FaultLog>(),
            sp.GetRequiredService<StationStatus>(),
            imageStaging.Enabled ? sp.GetRequiredService<ImageStager>() : null,
            sp.GetRequiredService<CounterStore>(),
            station, ifactory, camera, sequence, ini,
            sp.GetRequiredService<ILogger<StationSequencer>>(),
            TimeProvider.System));

        if (simulation.Enabled)
        {
            // Everything external replaced by in-process stand-ins driven from the SIMULATOR window.
            builder.Services.AddSingleton<SimulatedScannerReader>();
            builder.Services.AddSingleton<IScannerReader>(sp => sp.GetRequiredService<SimulatedScannerReader>());
            builder.Services.AddSingleton<SimulatedMesClient>();
            builder.Services.AddSingleton<IMesClient>(sp => sp.GetRequiredService<SimulatedMesClient>());
            builder.Services.AddSingleton<SimulatedLightGuide>();
            builder.Services.AddSingleton<ILightGuideVariableSource>(sp => sp.GetRequiredService<SimulatedLightGuide>());
            builder.Services.AddTransient<SimulatorView>();
        }
        else
        {
            builder.Services.AddHttpClient<IFactoryClient>(c => c.Timeout = TimeSpan.FromSeconds(ifactory.RequestTimeoutSeconds));
            builder.Services.AddSingleton<IMesClient>(sp => sp.GetRequiredService<IFactoryClient>());
            builder.Services.AddHttpClient<JabilEyeCameraClient>(c => c.Timeout = TimeSpan.FromSeconds(camera.RequestTimeoutSeconds));
            builder.Services.AddHttpClient<LightGuideClient>(c => c.Timeout = TimeSpan.FromSeconds(lightGuide.RequestTimeoutSeconds));
            builder.Services.AddSingleton<ILightGuideVariableSource>(sp => sp.GetRequiredService<LightGuideClient>());

            builder.Services.AddSingleton<IScannerReader>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return scanner.Mode.Equals("Serial", StringComparison.OrdinalIgnoreCase)
                    ? new SerialScannerReader(scanner, loggerFactory.CreateLogger<SerialScannerReader>())
                    : new HidScannerReader(scanner, loggerFactory.CreateLogger<HidScannerReader>());
            });

            builder.Services.AddHostedService<CameraWorker>();
        }

        builder.Services.AddHostedService<BadgeWorker>();
        builder.Services.AddHostedService<LightGuideWorker>();
        builder.Services.AddHostedService<HousekeepingWorker>();

        builder.Services.AddSingleton<Main>();

        return builder.Build();
    }
}

/// <summary>Which iFactory environment (STG/PRD) the station INI selected.</summary>
public record EnvironmentSelection(string Name, IFactoryEnvironment Urls);
