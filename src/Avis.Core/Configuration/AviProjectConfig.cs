namespace Avis.Configuration;

public record Recipe(string PartNumber, string Model);

public record JabilEyeHost(string Hostname, string Ip);

public record VisualAid(string Model, int? Step, string Url);

public enum AviProjectConfigSource
{
    /// <summary>Read from the configured INI path (normally the network share).</summary>
    Primary,

    /// <summary>The share couldn't be read - loaded from the last good local copy.</summary>
    Cache,

    /// <summary>Neither could be read - empty config, DefaultEnvironment used.</summary>
    Defaults,
}

/// <summary>
/// The shared station INI (AVIPROJECT_INI.txt). Sections:
/// <code>
/// [CONFIG]      STG = TRUE|FALSE
/// [RECIPE]      PartNumber = Model
/// [JABIL_EYE]   StationPcHostname = CameraIp
/// [VISUAL_ADD]  Model = Url            (shown for every step)
///               Model:Step = Url       (shown for that LightGuide step only)
/// </code>
/// Section names and keys are case-insensitive, values may contain '=' (URLs
/// with query strings), and lines starting with ';' or '#' are comments.
/// </summary>
public class AviProjectConfig
{
    public bool? Stg { get; set; }
    public List<Recipe> Recipes { get; } = new();
    public List<JabilEyeHost> JabilEyeHosts { get; } = new();
    public List<VisualAid> VisualAids { get; } = new();
    public List<string> Warnings { get; } = new();
    public AviProjectConfigSource Source { get; set; } = AviProjectConfigSource.Primary;

    /// <summary>Model for a part number (WIP material), or null when it has no recipe.</summary>
    public string? FindModel(string? partNumber) =>
        string.IsNullOrWhiteSpace(partNumber)
            ? null
            : Recipes.FirstOrDefault(r => r.PartNumber.Equals(partNumber.Trim(), StringComparison.OrdinalIgnoreCase))?.Model;

    /// <summary>Camera IP configured for this station PC, or null when the INI has no entry for it.</summary>
    public string? FindJabilEyeIp(string machineName) =>
        JabilEyeHosts.FirstOrDefault(h => h.Hostname.Equals(machineName, StringComparison.OrdinalIgnoreCase))?.Ip;

    /// <summary>The step-specific visual aid if there is one, else the model-wide one, else null.</summary>
    public string? FindVisualAid(string? model, int? step)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }
        var forModel = VisualAids.Where(v => v.Model.Equals(model.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return (step is not null ? forModel.FirstOrDefault(v => v.Step == step) : null)?.Url
            ?? forModel.FirstOrDefault(v => v.Step is null)?.Url;
    }

    public static AviProjectConfig Parse(IEnumerable<string> lines)
    {
        var config = new AviProjectConfig();
        var section = "";
        var lineNumber = 0;

        foreach (var raw in lines)
        {
            lineNumber++;
            var text = raw.Trim();
            if (text.Length == 0 || text.StartsWith(';') || text.StartsWith('#'))
            {
                continue;
            }

            if (text.StartsWith('[') && text.EndsWith(']'))
            {
                section = text[1..^1].Trim().ToUpperInvariant().Replace(" ", "");
                continue;
            }

            // Split on the FIRST '=' only - visual-aid URLs routinely contain '='.
            var separator = text.IndexOf('=');
            if (separator <= 0)
            {
                config.Warnings.Add($"Line {lineNumber}: expected KEY = VALUE, got '{text}'");
                continue;
            }
            var key = text[..separator].Trim();
            var value = text[(separator + 1)..].Trim();

            switch (section)
            {
                case "CONFIG":
                    if (key.Equals("STG", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TextBool.TryParse(value, out var stg))
                        {
                            config.Stg = stg;
                        }
                        else
                        {
                            config.Warnings.Add($"Line {lineNumber}: STG value '{value}' is not TRUE/FALSE");
                        }
                    }
                    break;

                case "RECIPE":
                    config.Recipes.Add(new Recipe(key, value));
                    break;

                // The original BaseProgram looked for "[JABI_LEYE]" (typo) - accept both spellings.
                case "JABIL_EYE":
                case "JABILEYE":
                case "JABI_LEYE":
                    config.JabilEyeHosts.Add(new JabilEyeHost(key, value));
                    break;

                case "VISUAL_ADD":
                case "VISUAL_AID":
                    var colon = key.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(key[(colon + 1)..].Trim(), out var step))
                    {
                        config.VisualAids.Add(new VisualAid(key[..colon].Trim(), step, value));
                    }
                    else
                    {
                        config.VisualAids.Add(new VisualAid(key, null, value));
                    }
                    break;

                default:
                    config.Warnings.Add($"Line {lineNumber}: '{text}' is outside a known section ([{section}])");
                    break;
            }
        }

        return config;
    }

    /// <summary>
    /// Loads the INI from <paramref name="primaryPath"/>, refreshing the local cache on
    /// success; falls back to the cache, then to an empty config. Never throws.
    /// </summary>
    public static AviProjectConfig Load(string primaryPath, string cachePath)
    {
        string? primaryError = null;
        if (!string.IsNullOrWhiteSpace(primaryPath))
        {
            try
            {
                var lines = File.ReadAllLines(primaryPath);
                var config = Parse(lines);
                config.Source = AviProjectConfigSource.Primary;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cachePath))!);
                    File.WriteAllLines(cachePath, lines);
                }
                catch (Exception ex)
                {
                    config.Warnings.Add($"Could not refresh the local INI cache at {cachePath}: {ex.Message}");
                }
                return config;
            }
            catch (Exception ex)
            {
                primaryError = $"Could not read station INI {primaryPath}: {ex.Message}";
            }
        }
        else
        {
            primaryError = "Avis:IniPath is not configured.";
        }

        try
        {
            if (File.Exists(cachePath))
            {
                var config = Parse(File.ReadAllLines(cachePath));
                config.Source = AviProjectConfigSource.Cache;
                config.Warnings.Insert(0, primaryError + " Using the last good local copy.");
                return config;
            }
        }
        catch (Exception ex)
        {
            primaryError += $" Local cache {cachePath} also unreadable: {ex.Message}";
        }

        var empty = new AviProjectConfig { Source = AviProjectConfigSource.Defaults };
        empty.Warnings.Add(primaryError + " No local copy available - running with defaults.");
        return empty;
    }
}
