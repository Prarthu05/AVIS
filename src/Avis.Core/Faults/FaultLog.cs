using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Avis.Faults;

public record FaultRecord(
    DateTimeOffset Timestamp,
    string Station,
    string Code,
    string IntegrationPoint,
    FaultSeverity Severity,
    string Title,
    string Message,
    string? AssetId = null,
    string? OperatorId = null,
    long? WipId = null);

public class FaultLogOptions
{
    public const string SectionName = "FaultLog";

    /// <summary>Local folder for the daily faults-yyyyMMdd.jsonl files. Relative paths resolve against the app folder.</summary>
    public string Directory { get; set; } = "faults";

    /// <summary>
    /// Optional second folder (typically a network share) every fault is also
    /// appended to, so a central dashboard can read all stations' feeds. A failure
    /// to write here never blocks the station - it's logged and skipped.
    /// </summary>
    public string? SharedDirectory { get; set; }

    /// <summary>How many recent faults the in-app fault feed keeps in memory.</summary>
    public int MaxInMemory { get; set; } = 500;
}

/// <summary>
/// The station's fault log: every fault from every integration point lands here
/// with its error code, is appended as one JSON line to a daily file (and
/// optionally a shared folder for the engineers' dashboard), and is raised as an
/// event so the in-app fault feed updates live.
/// </summary>
public class FaultLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly FaultLogOptions _options;
    private readonly string _station;
    private readonly ILogger<FaultLog> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private readonly LinkedList<FaultRecord> _recent = new();

    public event Action<FaultRecord>? FaultRecorded;

    public FaultLog(FaultLogOptions options, string station, ILogger<FaultLog> logger, TimeProvider timeProvider)
    {
        _options = options;
        _station = station;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<FaultRecord> Recent
    {
        get
        {
            lock (_lock)
            {
                return _recent.ToList();
            }
        }
    }

    public FaultRecord Record(
        FaultCode code,
        FaultSeverity severity,
        string message,
        string? assetId = null,
        string? operatorId = null,
        long? wipId = null)
    {
        var record = new FaultRecord(
            _timeProvider.GetLocalNow(),
            _station,
            code.ToDisplayCode(),
            code.ToIntegrationPoint(),
            severity,
            code.ToTitle(),
            message,
            assetId,
            operatorId,
            wipId);

        _logger.Log(ToLogLevel(severity), "FAULT {Code} {Title}: {Message} (asset={AssetId} operator={OperatorId} wip={WipId})",
            record.Code, record.Title, message, assetId, operatorId, wipId);

        var line = JsonSerializer.Serialize(record, JsonOptions);
        var fileName = $"faults-{record.Timestamp:yyyyMMdd}.jsonl";

        lock (_lock)
        {
            _recent.AddFirst(record);
            while (_recent.Count > Math.Max(1, _options.MaxInMemory))
            {
                _recent.RemoveLast();
            }

            AppendLine(_options.Directory, fileName, line);
            if (!string.IsNullOrWhiteSpace(_options.SharedDirectory))
            {
                // Station-prefixed so several stations can share one folder.
                AppendLine(_options.SharedDirectory, $"{Sanitize(_station)}-{fileName}", line);
            }
        }

        try
        {
            FaultRecorded?.Invoke(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A fault feed subscriber threw while handling fault {Code}", record.Code);
        }
        return record;
    }

    private void AppendLine(string directory, string fileName, string line)
    {
        try
        {
            var dir = Path.IsPathRooted(directory) ? directory : Path.Combine(AppContext.BaseDirectory, directory);
            System.IO.Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, fileName), line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // The fault log must never take the station down with it.
            _logger.LogWarning(ex, "Could not append to fault log in {Directory}", directory);
        }
    }

    internal static string Sanitize(string value) =>
        string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));

    private static LogLevel ToLogLevel(FaultSeverity severity) => severity switch
    {
        FaultSeverity.Info => LogLevel.Information,
        FaultSeverity.Warning => LogLevel.Warning,
        FaultSeverity.Error => LogLevel.Error,
        _ => LogLevel.Critical,
    };
}
