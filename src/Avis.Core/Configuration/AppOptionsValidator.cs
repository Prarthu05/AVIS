using Avis.Dashboard;
using Avis.LightGuide;

namespace Avis.Configuration;

public class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(string message) : base(message) { }
}

public static class AppOptionsValidator
{
    private static readonly string[] ValidScannerModes = { "Serial", "Hid" };

    /// <param name="requireIFactory">False in simulation mode, where iFactory is simulated and needs no credentials.</param>
    public static void Validate(
        IFactoryOptions ifactory,
        ScannerOptions scanner,
        JabilEyeCameraOptions camera,
        LightGuideOptions lightGuide,
        bool requireIFactory = true)
    {
        var errors = new List<string>();

        if (requireIFactory && (string.IsNullOrWhiteSpace(ifactory.Username) || string.IsNullOrWhiteSpace(ifactory.Password)))
        {
            errors.Add("IFACTORY_USERNAME / IFACTORY_PASSWORD environment variables must be set.");
        }

        if (requireIFactory && string.IsNullOrWhiteSpace(ifactory.BaseUrl))
        {
            errors.Add("IFactory:BaseUrl is required.");
        }

        if (!ValidScannerModes.Contains(scanner.Mode, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Scanner:Mode must be 'Serial' or 'Hid', got '{scanner.Mode}'.");
        }

        if (string.IsNullOrWhiteSpace(camera.HostName))
        {
            errors.Add("JabilEyeCamera:HostName is required (the camera's hostname or IP address).");
        }

        if (string.IsNullOrWhiteSpace(camera.ProgramName))
        {
            errors.Add("JabilEyeCamera:ProgramName is required (must match the program name configured in the JabilEye UI).");
        }

        if (string.IsNullOrWhiteSpace(camera.ResourceName))
        {
            errors.Add("JabilEyeCamera:ResourceName is required (the iFactory resource this camera's captures Start WIP against).");
        }

        if (lightGuide.Enabled)
        {
            if (!Uri.TryCreate(lightGuide.BaseUrl, UriKind.Absolute, out _))
            {
                errors.Add($"LightGuide:BaseUrl '{lightGuide.BaseUrl}' is not a valid URL (e.g. http://localhost:54274).");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var check in lightGuide.Checks)
            {
                if (string.IsNullOrWhiteSpace(check.Name))
                {
                    errors.Add("Every LightGuide:Checks entry needs a Name.");
                }
                else if (!names.Add(check.Name))
                {
                    errors.Add($"LightGuide:Checks has two checks named '{check.Name}'.");
                }
                if (string.IsNullOrWhiteSpace(check.ResultVariable))
                {
                    errors.Add($"LightGuide check '{check.Name}' needs a ResultVariable.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException("Invalid configuration:\n- " + string.Join("\n- ", errors));
        }
    }

    /// <summary>Only checked when the dashboard is switched on - a station without it runs exactly as before.</summary>
    public static void ValidateDashboard(DashboardOptions dashboard)
    {
        if (!dashboard.Enabled)
        {
            return;
        }

        var errors = new List<string>();
        if (!Uri.TryCreate(dashboard.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add($"Dashboard:BaseUrl '{dashboard.BaseUrl}' is not a valid URL (e.g. http://10.77.193.155:3230).");
        }
        if (string.IsNullOrWhiteSpace(dashboard.ApiKey))
        {
            errors.Add("AVIS_DASHBOARD_KEY environment variable must be set when Dashboard:Enabled is true (same value as AVIS_STATION_KEY on the dashboard).");
        }
        if (string.IsNullOrWhiteSpace(dashboard.StationName))
        {
            errors.Add("Dashboard:StationName (or Station:Name) is required.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException("Invalid configuration:\n- " + string.Join("\n- ", errors));
        }
    }
}
