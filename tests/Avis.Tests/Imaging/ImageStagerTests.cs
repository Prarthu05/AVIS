using System.Text.Json;
using Avis.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Imaging;

public sealed class ImageStagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "avis-img-" + Guid.NewGuid().ToString("N"));
    private readonly string _drop;
    private readonly ImageStager _stager;

    public ImageStagerTests()
    {
        _drop = Path.Combine(_root, "drop");
        Directory.CreateDirectory(_drop);
        _stager = new ImageStager(
            new ImageStagingOptions { DropFolder = _drop, StagingFolder = Path.Combine(_root, "staged"), WaitSeconds = 0.5, RetentionDays = 30 },
            NullLogger<ImageStager>.Instance);
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static ImageTag Tag(bool passed = false) => new(
        "ASSET/1", "4375789", 101, "CVG300", 4, "IV4", 2, passed, passed ? "OK" : "NG",
        new DateTimeOffset(2026, 10, 5, 9, 30, 15, 123, TimeSpan.Zero));

    [Fact]
    public async Task StagesTheNewestImage_TaggedWithAssetId_WithSidecar()
    {
        var old = Path.Combine(_drop, "old.bmp");
        File.WriteAllText(old, "old");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddMinutes(-10));
        var since = DateTimeOffset.UtcNow;
        File.WriteAllText(Path.Combine(_drop, "IV4_0001.bmp"), "new");

        var staged = await _stager.StageLatestAsync(Tag(), since, CancellationToken.None);

        Assert.NotNull(staged);
        Assert.Equal("ASSET_1_20261005-093015-123_IV4_A2_NG.bmp", Path.GetFileName(staged));
        Assert.Contains(Path.Combine("2026-10-05", "ASSET_1"), staged);
        Assert.Equal("new", File.ReadAllText(staged!));
        Assert.False(File.Exists(Path.Combine(_drop, "IV4_0001.bmp")));
        Assert.True(File.Exists(old)); // older image belongs to an earlier attempt - left alone

        using var sidecar = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(staged!, ".json")));
        Assert.Equal("ASSET/1", sidecar.RootElement.GetProperty("AssetId").GetString());
        Assert.Equal("4375789", sidecar.RootElement.GetProperty("OperatorId").GetString());
        Assert.Equal("NG", sidecar.RootElement.GetProperty("Result").GetString());
        Assert.Equal("IV4_0001.bmp", sidecar.RootElement.GetProperty("OriginalFileName").GetString());
    }

    [Fact]
    public async Task ReturnsNull_WhenNoNewImageArrives()
    {
        var staged = await _stager.StageLatestAsync(Tag(), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Null(staged);
    }

    [Fact]
    public async Task WaitsForAnImageThatArrivesLate()
    {
        var since = DateTimeOffset.UtcNow;
        var stage = _stager.StageLatestAsync(Tag(passed: true), since, CancellationToken.None);
        await Task.Delay(150);
        File.WriteAllText(Path.Combine(_drop, "late.png"), "x");

        var staged = await stage;

        Assert.EndsWith("_OK.png", staged);
    }

    [Fact]
    public async Task MissingDropFolder_Throws()
    {
        var stager = new ImageStager(new ImageStagingOptions { DropFolder = Path.Combine(_root, "missing") }, NullLogger<ImageStager>.Instance);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => stager.StageLatestAsync(Tag(), DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public void Cleanup_DeletesOnlyDayFoldersOlderThanRetention()
    {
        var staged = Path.Combine(_root, "staged");
        Directory.CreateDirectory(Path.Combine(staged, "2026-08-01", "A"));
        Directory.CreateDirectory(Path.Combine(staged, "2026-10-01", "A"));
        Directory.CreateDirectory(Path.Combine(staged, "not-a-date"));

        _stager.CleanupOldImages(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));

        Assert.False(Directory.Exists(Path.Combine(staged, "2026-08-01")));
        Assert.True(Directory.Exists(Path.Combine(staged, "2026-10-01")));
        Assert.True(Directory.Exists(Path.Combine(staged, "not-a-date")));
    }
}
