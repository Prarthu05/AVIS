using System.Text.Json;
using Avis.Faults;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Faults;

public sealed class FaultLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avis-faults-" + Guid.NewGuid().ToString("N"));
    private readonly string _shared = Path.Combine(Path.GetTempPath(), "avis-faults-shared-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        foreach (var d in new[] { _dir, _shared })
        {
            if (Directory.Exists(d))
            {
                Directory.Delete(d, true);
            }
        }
    }

    [Fact]
    public void Record_AppendsJsonLine_KeepsItInMemory_AndRaisesEvent()
    {
        var log = new FaultLog(new FaultLogOptions { Directory = _dir, SharedDirectory = _shared }, "Station 2", NullLogger<FaultLog>.Instance, TimeProvider.System);
        FaultRecord? raised = null;
        log.FaultRecorded += r => raised = r;

        var record = log.Record(FaultCode.StartWipRejected, FaultSeverity.Error, "rejected", "ASSET1", "4375789", 42);

        Assert.Same(record, raised);
        Assert.Equal("IF-03", record.Code);
        Assert.Equal("iFactory", record.IntegrationPoint);
        Assert.Single(log.Recent);

        var file = Assert.Single(Directory.GetFiles(_dir, "faults-*.jsonl"));
        using var doc = JsonDocument.Parse(File.ReadAllLines(file).Single());
        Assert.Equal("IF-03", doc.RootElement.GetProperty("Code").GetString());
        Assert.Equal("Error", doc.RootElement.GetProperty("Severity").GetString());
        Assert.Equal("ASSET1", doc.RootElement.GetProperty("AssetId").GetString());

        var sharedFile = Assert.Single(Directory.GetFiles(_shared));
        Assert.StartsWith("Station_2-faults-", Path.GetFileName(sharedFile));
    }

    [Fact]
    public void Recent_IsNewestFirst_AndCapped()
    {
        var log = new FaultLog(new FaultLogOptions { Directory = _dir, MaxInMemory = 2 }, "S", NullLogger<FaultLog>.Instance, TimeProvider.System);

        log.Record(FaultCode.CameraUnreachable, FaultSeverity.Error, "1");
        log.Record(FaultCode.CameraUnreachable, FaultSeverity.Error, "2");
        log.Record(FaultCode.CameraUnreachable, FaultSeverity.Error, "3");

        Assert.Equal(new[] { "3", "2" }, log.Recent.Select(r => r.Message));
    }

    [Fact]
    public void UnwritableSharedFolder_NeverThrows()
    {
        var log = new FaultLog(new FaultLogOptions { Directory = _dir, SharedDirectory = "\0invalid" }, "S", NullLogger<FaultLog>.Instance, TimeProvider.System);

        var record = log.Record(FaultCode.CameraUnreachable, FaultSeverity.Error, "still recorded");

        Assert.Equal("still recorded", record.Message);
        Assert.Single(Directory.GetFiles(_dir));
    }
}
