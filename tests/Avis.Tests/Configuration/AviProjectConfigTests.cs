using Avis.Configuration;
using Xunit;

namespace Avis.Tests.Configuration;

public class AviProjectConfigTests
{
    private static readonly string[] SampleIni =
    {
        "; station INI",
        "[CONFIG]",
        "STG = TRUE",
        "",
        "[RECIPE]",
        "PN-1001 = CVG300-A",
        "[JABI_LEYE]",
        "AVIS-PC-02 = 10.72.194.166",
        "[visual_add]",
        "CVG300-A = https://va.example/model?doc=1&rev=B",
        "CVG300-A:3 = https://va.example/step3?x=1",
    };

    [Fact]
    public void Parse_ReadsAllSections()
    {
        var config = AviProjectConfig.Parse(SampleIni);

        Assert.True(config.Stg);
        Assert.Equal("CVG300-A", config.FindModel("pn-1001"));
        Assert.Equal("10.72.194.166", config.FindJabilEyeIp("avis-pc-02"));
        Assert.Empty(config.Warnings);
    }

    [Fact]
    public void Parse_KeepsEqualsSignsInsideValues()
    {
        // The original parser split on every '=' and silently dropped URLs with a query string.
        var config = AviProjectConfig.Parse(SampleIni);

        Assert.Equal("https://va.example/model?doc=1&rev=B", config.FindVisualAid("CVG300-A", null));
    }

    [Fact]
    public void FindVisualAid_PrefersStepSpecificEntry_ThenModelWide()
    {
        var config = AviProjectConfig.Parse(SampleIni);

        Assert.Equal("https://va.example/step3?x=1", config.FindVisualAid("CVG300-A", 3));
        Assert.Equal("https://va.example/model?doc=1&rev=B", config.FindVisualAid("CVG300-A", 4));
        Assert.Null(config.FindVisualAid("OTHER", 3));
        Assert.Null(config.FindVisualAid(null, 3));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("False", false)]
    [InlineData("0", false)]
    public void Parse_StgIsLenient(string value, bool expected)
    {
        var config = AviProjectConfig.Parse(new[] { "[CONFIG]", $"STG={value}" });

        Assert.Equal(expected, config.Stg);
    }

    [Fact]
    public void Parse_InvalidStg_IsAWarningNotACrash()
    {
        var config = AviProjectConfig.Parse(new[] { "[CONFIG]", "STG = maybe", "garbage line" });

        Assert.Null(config.Stg);
        Assert.Equal(2, config.Warnings.Count);
    }

    [Fact]
    public void Load_FallsBackToCache_WhenPrimaryIsMissing_AndRefreshesCacheOnSuccess()
    {
        var dir = Path.Combine(Path.GetTempPath(), "avis-ini-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var primary = Path.Combine(dir, "primary.txt");
            var cache = Path.Combine(dir, "cache", "ini.txt");
            File.WriteAllLines(primary, SampleIni);

            var fromPrimary = AviProjectConfig.Load(primary, cache);
            Assert.Equal(AviProjectConfigSource.Primary, fromPrimary.Source);
            Assert.True(File.Exists(cache));

            File.Delete(primary);
            var fromCache = AviProjectConfig.Load(primary, cache);
            Assert.Equal(AviProjectConfigSource.Cache, fromCache.Source);
            Assert.True(fromCache.Stg);
            Assert.NotEmpty(fromCache.Warnings);

            File.Delete(cache);
            var defaults = AviProjectConfig.Load(primary, cache);
            Assert.Equal(AviProjectConfigSource.Defaults, defaults.Source);
            Assert.Null(defaults.Stg);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
