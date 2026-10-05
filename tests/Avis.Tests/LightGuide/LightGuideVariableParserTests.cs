using Avis.LightGuide;
using Xunit;

namespace Avis.Tests.LightGuide;

public class LightGuideVariableParserTests
{
    private static readonly string[] Names = { "WI_Name", "StepNumberMain" };

    [Fact]
    public void Parses_NameValueArray()
    {
        var values = LightGuideVariableParser.Parse(
            """[{"Name":"WI_Name","Value":"CVG300"},{"name":"StepNumberMain","value":3}]""", Names);

        Assert.Equal("CVG300", values["wi_name"]);
        Assert.Equal("3", values["StepNumberMain"]);
    }

    [Fact]
    public void Parses_ObjectMap()
    {
        var values = LightGuideVariableParser.Parse("""{"WI_Name":"CVG300","StepNumberMain":"3"}""", Names);

        Assert.Equal("CVG300", values["WI_Name"]);
        Assert.Equal("3", values["StepNumberMain"]);
    }

    [Fact]
    public void Parses_ObjectMapOfVariableObjects()
    {
        var values = LightGuideVariableParser.Parse("""{"WI_Name":{"Type":"Text","Value":"CVG300"}}""", Names);

        Assert.Equal("CVG300", values["WI_Name"]);
    }

    [Fact]
    public void Parses_ValueArrayInRequestOrder()
    {
        var values = LightGuideVariableParser.Parse("""["CVG300", 3]""", Names);

        Assert.Equal("CVG300", values["WI_Name"]);
        Assert.Equal("3", values["StepNumberMain"]);
    }

    [Theory]
    [InlineData("\"OK\"", "OK")]
    [InlineData("OK", "OK")]
    [InlineData("true", "true")]
    [InlineData("{\"Name\":\"AVIS_LJ_Result\",\"Value\":\"NG\"}", "NG")]
    public void Parses_SingleVariable(string body, string expected)
    {
        var values = LightGuideVariableParser.Parse(body, new[] { "AVIS_LJ_Result" });

        Assert.Equal(expected, values["AVIS_LJ_Result"]);
    }

    [Fact]
    public void NullValues_AreNull()
    {
        var values = LightGuideVariableParser.Parse("""[{"Name":"WI_Name","Value":null}]""", Names);

        Assert.Null(values["WI_Name"]);
        Assert.False(values.ContainsKey("StepNumberMain"));
    }
}
