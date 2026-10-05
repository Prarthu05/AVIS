namespace Avis.Configuration;

public class IFactoryEnvironment
{
    /// <summary>iFactory REST API base URL, e.g. https://stg.ifactory.ken.corp.jabil.org:60200</summary>
    public string ApiBaseUrl { get; set; } = "";

    /// <summary>iFactory web UI shown in the embedded browser panel.</summary>
    public string WebUrl { get; set; } = "";
}

/// <summary>Application-level AVIS settings (appsettings.json "Avis" section).</summary>
public class AvisOptions
{
    public const string SectionName = "Avis";

    /// <summary>The shared station INI (STG flag, recipes, JabilEye hosts, visual aids) on the network share.</summary>
    public string IniPath { get; set; } = "";

    /// <summary>
    /// Local copy of the INI, refreshed on every successful load. Used when the
    /// share can't be reached so a network blip doesn't stop the station from starting.
    /// Relative paths resolve against the app folder.
    /// </summary>
    public string IniCachePath { get; set; } = "config/AVIPROJECT_INI.cache.txt";

    /// <summary>"STG" or "PRD" - used only when neither the INI nor its cache could be read.</summary>
    public string DefaultEnvironment { get; set; } = "STG";

    public IFactoryEnvironment Stg { get; set; } = new();
    public IFactoryEnvironment Prd { get; set; } = new();

    public double WebZoomFactor { get; set; } = 0.8;

    /// <summary>
    /// WebView2 profile folder (cookies / iFactory login). Blank = per-Windows-user
    /// %LOCALAPPDATA%\AVIS\WebView2, which is always writable. Point it at a shared
    /// folder only if every operator account has modify rights to it.
    /// </summary>
    public string WebView2UserDataFolder { get; set; } = "";

    /// <summary>Folder for the daily PASS/FAIL counters. Relative paths resolve against the app folder.</summary>
    public string DataDirectory { get; set; } = "data";

    /// <summary>Switch to the Visual Aid tab automatically when LightGuide reports a step with a visual aid.</summary>
    public bool AutoShowVisualAid { get; set; } = true;

    public static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
