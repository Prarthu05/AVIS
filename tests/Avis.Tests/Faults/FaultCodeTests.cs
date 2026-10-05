using Avis.Faults;
using Xunit;

namespace Avis.Tests.Faults;

public class FaultCodeTests
{
    [Fact]
    public void EveryFaultCode_HasAUniqueDisplayCode_TitleAndIntegrationPoint()
    {
        var codes = Enum.GetValues<FaultCode>();

        Assert.Equal(codes.Length, codes.Select(c => c.ToDisplayCode()).Distinct().Count());
        Assert.DoesNotContain(codes, c => c.ToDisplayCode() == "UNK-00");
        Assert.DoesNotContain(codes, c => c.ToTitle() == "Unknown Fault");
        Assert.DoesNotContain(codes, c => c.ToIntegrationPoint() == "Unknown");
    }

    [Theory]
    [InlineData(FaultCode.CameraUnreachable, "JE-01", "JabilEye")]
    [InlineData(FaultCode.StartWipRejected, "IF-03", "iFactory")]
    [InlineData(FaultCode.LightGuideUnreachable, "LG-01", "LightGuide")]
    [InlineData(FaultCode.LjCheckFailed, "LJ-01", "LJ")]
    [InlineData(FaultCode.Iv4CheckFailed, "IV-01", "IV4")]
    [InlineData(FaultCode.ImageNotFound, "IMG-01", "Image Staging")]
    [InlineData(FaultCode.ReworkLimitReached, "ST-01", "Station")]
    [InlineData(FaultCode.NoRecentBadgeScan, "SCN-01", "Badge Scanner")]
    public void FaultCodes_MapToTheirIntegrationPoint(FaultCode code, string display, string point)
    {
        Assert.Equal(display, code.ToDisplayCode());
        Assert.Equal(point, code.ToIntegrationPoint());
    }
}
