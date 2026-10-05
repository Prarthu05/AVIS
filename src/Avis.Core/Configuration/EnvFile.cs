namespace Avis.Configuration;

/// <summary>
/// Loads KEY=VALUE lines from a local .env file into process environment variables,
/// for local/bench-test convenience (mirrors python-dotenv). Only fills variables
/// that aren't already set, so real environment variables (as used in the actual
/// Task Scheduler deployment) always take precedence.
/// </summary>
public static class EnvFile
{
    /// <summary>
    /// Searches the current directory and up to 6 levels above the running
    /// binary for a .env file. `dotnet run --project &lt;subfolder&gt;` and a
    /// published exe run from its own folder can each land on a different
    /// working directory, so a single fixed relative path isn't reliable.
    /// </summary>
    public static void Load()
    {
        var path = FindEnvFile();
        if (path is null)
        {
            return;
        }
        LoadFile(path);
    }

    private static string? FindEnvFile()
    {
        var candidates = new List<string> { Path.Combine(Directory.GetCurrentDirectory(), ".env") };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent!)
        {
            candidates.Add(Path.Combine(dir.FullName, ".env"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
