using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Avis.Dashboard;

/// <summary>
/// Store-and-forward queue for dashboard events. Every event is appended to a
/// JSONL file before anything is sent, so a dashboard outage, network drop or
/// station restart never loses data; the uploader acknowledges what the
/// dashboard accepted and the file is rewritten without it. The dashboard
/// de-duplicates by event id, so a batch re-sent after a timeout is harmless.
/// </summary>
public class EventOutbox : IStationEventSink
{
    internal static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly string _path;
    private readonly int _maxEvents;
    private readonly ILogger<EventOutbox> _logger;
    private readonly object _lock = new();
    private readonly List<StationEvent> _pending = new();
    private int _droppedSinceLastWarning;

    public EventOutbox(string path, int maxEvents, ILogger<EventOutbox> logger)
    {
        _path = path;
        _maxEvents = Math.Max(100, maxEvents);
        _logger = logger;
        Load();
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _pending.Count;
            }
        }
    }

    public void Emit(StationEvent e)
    {
        lock (_lock)
        {
            _pending.Add(e);
            if (_pending.Count > _maxEvents)
            {
                var drop = _pending.Count - _maxEvents;
                _pending.RemoveRange(0, drop);
                _droppedSinceLastWarning += drop;
                Rewrite();
                if (_droppedSinceLastWarning >= 1000 || _droppedSinceLastWarning == drop)
                {
                    _logger.LogWarning("Dashboard unreachable for a long time - dropped {Count} oldest queued events", _droppedSinceLastWarning);
                    _droppedSinceLastWarning = 0;
                }
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
                File.AppendAllText(_path, JsonSerializer.Serialize(e, Json) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Still queued in memory - only a restart before upload would lose it.
                _logger.LogWarning(ex, "Could not persist dashboard event to {Path}", _path);
            }
        }
    }

    /// <summary>The oldest events, up to <paramref name="max"/>, without removing them.</summary>
    public IReadOnlyList<StationEvent> Peek(int max)
    {
        lock (_lock)
        {
            return _pending.Take(max).ToList();
        }
    }

    /// <summary>Removes the events the dashboard accepted (matched by id, so events emitted meanwhile are kept).</summary>
    public void Acknowledge(IReadOnlyCollection<StationEvent> sent)
    {
        lock (_lock)
        {
            var ids = sent.Select(e => e.Id).ToHashSet();
            _pending.RemoveAll(e => ids.Contains(e.Id));
            Rewrite();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }
            foreach (var line in File.ReadLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                try
                {
                    var e = JsonSerializer.Deserialize<StationEvent>(line, Json);
                    if (e is not null)
                    {
                        _pending.Add(e);
                    }
                }
                catch (JsonException)
                {
                    // A torn last line after a power cut - skip it.
                }
            }
            if (_pending.Count > 0)
            {
                _logger.LogInformation("{Count} dashboard events waiting from before the restart", _pending.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the dashboard outbox {Path}", _path);
        }
    }

    private void Rewrite()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var tmp = _path + ".tmp";
            File.WriteAllLines(tmp, _pending.Select(e => JsonSerializer.Serialize(e, Json)));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not rewrite the dashboard outbox {Path}", _path);
        }
    }
}
