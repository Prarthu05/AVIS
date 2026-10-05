using System.Text.Json;
using Avis.Configuration;
using Microsoft.Extensions.Logging;

namespace Avis.Imaging;

public class ImageStagingOptions
{
    public const string SectionName = "ImageStaging";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Folder on the station PC the IV4 pushes its images into (its FTP/SMB image
    /// output target). AVIS takes the newest image from here after each IV4 result.
    /// </summary>
    public string DropFolder { get; set; } = @"C:\AVIS\IV4Drop";

    /// <summary>Where tagged images are kept: {StagingFolder}\{yyyy-MM-dd}\{AssetId}\... Relative paths resolve against the app folder.</summary>
    public string StagingFolder { get; set; } = "images";

    public List<string> FilePatterns { get; set; } = new();

    /// <summary>How long to wait for the IV4 image to land after LightGuide reports the IV4 result.</summary>
    public double WaitSeconds { get; set; } = 5;

    /// <summary>Delete staged day-folders older than this. 0 = keep forever.</summary>
    public int RetentionDays { get; set; } = 90;

    public IReadOnlyList<string> EffectiveFilePatterns =>
        FilePatterns.Count > 0 ? FilePatterns : new[] { "*.bmp", "*.jpg", "*.jpeg", "*.png" };
}

/// <summary>Everything an image is tagged with - written to its file name and a .json sidecar.</summary>
public record ImageTag(
    string AssetId,
    string? OperatorId,
    long? WipId,
    string? Program,
    int? Step,
    string CheckName,
    int Attempt,
    bool Passed,
    string ResultValue,
    DateTimeOffset CapturedAt);

/// <summary>
/// Picks up IV4 images the sensor pushed to the station PC and files them under
/// the part's Asset ID: {AssetId}_{timestamp}_{check}_A{attempt}_{OK|NG}.ext plus
/// a matching .json sidecar with the full tag (operator, WIP, program, step...).
/// </summary>
public class ImageStager
{
    private readonly ImageStagingOptions _options;
    private readonly ILogger<ImageStager> _logger;

    public ImageStager(ImageStagingOptions options, ILogger<ImageStager> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string StagingRoot => AvisOptions.ResolvePath(_options.StagingFolder);

    /// <summary>
    /// Waits up to WaitSeconds for an image newer than <paramref name="notBefore"/>
    /// in the drop folder and moves it into staging. Returns the staged path, or
    /// null when no new image arrived in time.
    /// </summary>
    public async Task<string?> StageLatestAsync(ImageTag tag, DateTimeOffset notBefore, CancellationToken ct)
    {
        var dropFolder = AvisOptions.ResolvePath(_options.DropFolder);
        if (!Directory.Exists(dropFolder))
        {
            throw new DirectoryNotFoundException($"IV4 image drop folder {dropFolder} does not exist");
        }

        // Small tolerance for coarse file-system timestamps.
        var threshold = notBefore.UtcDateTime.AddSeconds(-2);
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(0, _options.WaitSeconds));

        while (true)
        {
            var newest = _options.EffectiveFilePatterns
                .SelectMany(p => new DirectoryInfo(dropFolder).EnumerateFiles(p, SearchOption.TopDirectoryOnly))
                .Where(f => f.LastWriteTimeUtc >= threshold)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (newest is not null && await WaitUntilReadableAsync(newest.FullName, ct))
            {
                return Stage(newest.FullName, tag);
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }
            await Task.Delay(250, ct);
        }
    }

    internal string Stage(string sourcePath, ImageTag tag)
    {
        var asset = Sanitize(tag.AssetId);
        var folder = Path.Combine(StagingRoot, tag.CapturedAt.ToString("yyyy-MM-dd"), asset);
        Directory.CreateDirectory(folder);

        var baseName = $"{asset}_{tag.CapturedAt:yyyyMMdd-HHmmss-fff}_{Sanitize(tag.CheckName)}_A{tag.Attempt}_{(tag.Passed ? "OK" : "NG")}";
        var target = Path.Combine(folder, baseName + Path.GetExtension(sourcePath).ToLowerInvariant());
        File.Move(sourcePath, target, overwrite: true);

        var sidecar = new
        {
            tag.AssetId,
            tag.OperatorId,
            tag.WipId,
            tag.Program,
            tag.Step,
            Check = tag.CheckName,
            tag.Attempt,
            Result = tag.Passed ? "OK" : "NG",
            tag.ResultValue,
            tag.CapturedAt,
            OriginalFileName = Path.GetFileName(sourcePath),
        };
        File.WriteAllText(Path.ChangeExtension(target, ".json"),
            JsonSerializer.Serialize(sidecar, new JsonSerializerOptions { WriteIndented = true }));

        _logger.LogInformation("Staged IV4 image {Source} as {Target}", sourcePath, target);
        return target;
    }

    /// <summary>Deletes staged day-folders older than RetentionDays. Never throws.</summary>
    public void CleanupOldImages(DateTimeOffset now)
    {
        if (_options.RetentionDays <= 0 || !Directory.Exists(StagingRoot))
        {
            return;
        }
        var cutoff = now.Date.AddDays(-_options.RetentionDays);
        foreach (var dir in Directory.EnumerateDirectories(StagingRoot))
        {
            if (DateTime.TryParseExact(Path.GetFileName(dir), "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var day) && day < cutoff)
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    _logger.LogInformation("Deleted staged images older than {Days} days: {Dir}", _options.RetentionDays, dir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete old staged image folder {Dir}", dir);
                }
            }
        }
    }

    /// <summary>The IV4 may still be writing the file (FTP upload) - wait until it can be opened exclusively.</summary>
    private static async Task<bool> WaitUntilReadableAsync(string path, CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                await Task.Delay(100, ct);
            }
        }
        return false;
    }

    private static string Sanitize(string value)
    {
        var cleaned = string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        return cleaned.Length == 0 ? "UNKNOWN" : cleaned;
    }
}
