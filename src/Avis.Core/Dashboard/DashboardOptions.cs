namespace Avis.Dashboard;

/// <summary>
/// Connection to the AVIS dashboard on the Raspberry Pi (appsettings "Dashboard").
/// The shared station key comes from the AVIS_DASHBOARD_KEY environment variable,
/// never from appsettings.json.
/// </summary>
public class DashboardOptions
{
    public const string SectionName = "Dashboard";

    public bool Enabled { get; set; }

    /// <summary>e.g. http://10.77.193.155:3230</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Name this station reports as. Blank = Station:Name.</summary>
    public string StationName { get; set; } = "";

    /// <summary>Populated from AVIS_DASHBOARD_KEY at startup.</summary>
    public string ApiKey { get; set; } = "";

    public double RequestTimeoutSeconds { get; set; } = 10;
    public double UploadIntervalSeconds { get; set; } = 5;
    public double HeartbeatIntervalSeconds { get; set; } = 30;
    public double VaSyncIntervalSeconds { get; set; } = 60;

    /// <summary>Local copy of the approved VAs (map + page images). Relative paths resolve against the app folder.</summary>
    public string CacheDirectory { get; set; } = "va-cache";

    /// <summary>Events waiting to be uploaded survive restarts here.</summary>
    public string OutboxPath { get; set; } = "data/dashboard-outbox.jsonl";

    /// <summary>Oldest events are dropped beyond this many while the dashboard is unreachable (~a few days of production).</summary>
    public int MaxOutboxEvents { get; set; } = 50_000;
}
