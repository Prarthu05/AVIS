using Avis.Configuration;
using Avis.Scanner;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Scanner;

public class SerialScannerReaderTests
{
    private static SerialScannerReader MakeReader(string lineTerminator = "CR", string? employeeIdRegex = null)
    {
        var options = new ScannerOptions { LineTerminator = lineTerminator, EmployeeIdRegex = employeeIdRegex };
        return new SerialScannerReader(options, NullLogger<SerialScannerReader>.Instance);
    }

    private static List<byte> Bytes(string s) => new(System.Text.Encoding.UTF8.GetBytes(s));

    [Fact]
    public void ExtractsSingleCrTerminatedLine()
    {
        var reader = MakeReader("CR");
        var buffer = Bytes("4045423\r");

        reader.ExtractLines(buffer);

        Assert.Empty(buffer);
        Assert.True(reader.Reader.TryRead(out var value));
        Assert.Equal("4045423", value);
    }

    [Fact]
    public void BuffersPartialLineUntilTerminatorArrives()
    {
        var reader = MakeReader("CR");
        var buffer = Bytes("404");

        reader.ExtractLines(buffer);
        Assert.Equal(3, buffer.Count);
        Assert.False(reader.Reader.TryRead(out _));

        buffer.AddRange(Bytes("5423\r"));
        reader.ExtractLines(buffer);

        Assert.Empty(buffer);
        Assert.True(reader.Reader.TryRead(out var value));
        Assert.Equal("4045423", value);
    }

    [Fact]
    public void HandlesMultipleLinesInOneChunk()
    {
        var reader = MakeReader("CR");
        var buffer = Bytes("111\r222\r");

        reader.ExtractLines(buffer);

        Assert.True(reader.Reader.TryRead(out var first));
        Assert.Equal("111", first);
        Assert.True(reader.Reader.TryRead(out var second));
        Assert.Equal("222", second);
    }

    [Fact]
    public void CrlfTerminator()
    {
        var reader = MakeReader("CRLF");
        var buffer = Bytes("999\r\n");

        reader.ExtractLines(buffer);

        Assert.True(reader.Reader.TryRead(out var value));
        Assert.Equal("999", value);
    }

    [Fact]
    public void LfTerminator()
    {
        var reader = MakeReader("LF");
        var buffer = Bytes("555\n");

        reader.ExtractLines(buffer);

        Assert.True(reader.Reader.TryRead(out var value));
        Assert.Equal("555", value);
    }

    [Fact]
    public void EmployeeIdRegexExtractsCaptureGroup()
    {
        var reader = MakeReader("CR", employeeIdRegex: "^ID:(\\d+)$");
        var buffer = Bytes("ID:4045423\r");

        reader.ExtractLines(buffer);

        Assert.True(reader.Reader.TryRead(out var value));
        Assert.Equal("4045423", value);
    }

    [Fact]
    public void EmployeeIdRegexRejectsNonMatchingScan()
    {
        var reader = MakeReader("CR", employeeIdRegex: "^ID:(\\d+)$");
        var buffer = Bytes("garbage\r");

        reader.ExtractLines(buffer);

        Assert.False(reader.Reader.TryRead(out _));
    }

    [Fact]
    public void BlankLineIsIgnored()
    {
        var reader = MakeReader("CR");
        var buffer = Bytes("\r");

        reader.ExtractLines(buffer);

        Assert.False(reader.Reader.TryRead(out _));
    }

    [Fact]
    public void WhitespaceIsTrimmed()
    {
        var reader = MakeReader("CR");
        var buffer = Bytes("  4045423  \r");

        reader.ExtractLines(buffer);

        Assert.True(reader.Reader.TryRead(out var value));
        Assert.Equal("4045423", value);
    }
}
