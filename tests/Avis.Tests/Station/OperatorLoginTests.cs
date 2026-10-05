using Avis.Station;
using Xunit;

namespace Avis.Tests.Station;

public class OperatorLoginTests
{
    [Fact]
    public void Build_CombinesDomainAndEmployeeIdWithBackslash()
    {
        Assert.Equal(@"Jabil\4375789", OperatorLogin.Build("Jabil", "4375789"));
    }
}
