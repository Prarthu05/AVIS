using Avis.Scanner.Windows;
using Xunit;

namespace Avis.Tests.Scanner;

public class RawInputLineAssemblerTests
{
    private const bool KeyDown = false;
    private const bool KeyUp = true;

    private static string? FeedDigits(RawInputLineAssembler assembler, string digits)
    {
        string? result = null;
        foreach (var d in digits)
        {
            var vk = 0x30 + (d - '0'); // VK_0..VK_9
            result = assembler.Feed(vk, KeyDown);
            assembler.Feed(vk, KeyUp);
        }
        return result;
    }

    [Fact]
    public void DigitsThenEnter_ReturnsScannedString()
    {
        var assembler = new RawInputLineAssembler();
        Assert.Null(FeedDigits(assembler, "4375789"));

        var result = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);

        Assert.Equal("4375789", result);
    }

    [Fact]
    public void KeyUpAloneDoesNotAddCharacters()
    {
        var assembler = new RawInputLineAssembler();
        assembler.Feed(0x34, KeyUp); // VK_4 up, no matching down first

        var result = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);

        Assert.Null(result);
    }

    [Fact]
    public void ShiftProducesUppercaseLetters()
    {
        var assembler = new RawInputLineAssembler();
        assembler.Feed(VirtualKeyMap.VkLShift, KeyDown);
        assembler.Feed(0x41, KeyDown); // VK_A
        assembler.Feed(0x41, KeyUp);
        assembler.Feed(VirtualKeyMap.VkLShift, KeyUp);
        assembler.Feed(0x42, KeyDown); // VK_B
        assembler.Feed(0x42, KeyUp);

        var result = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);

        Assert.Equal("Ab", result);
    }

    [Fact]
    public void NumpadDigitsMapToPlainDigits()
    {
        var assembler = new RawInputLineAssembler();
        assembler.Feed(0x64, KeyDown); // VK_NUMPAD4
        assembler.Feed(0x64, KeyUp);
        assembler.Feed(0x65, KeyDown); // VK_NUMPAD5
        assembler.Feed(0x65, KeyUp);

        var result = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);

        Assert.Equal("45", result);
    }

    [Fact]
    public void UnknownVirtualKeyIsIgnoredNotAppended()
    {
        var assembler = new RawInputLineAssembler();
        assembler.Feed(0x70, KeyDown); // VK_F1 - not in the map
        FeedDigits(assembler, "9");

        var result = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);

        Assert.Equal("9", result);
    }

    [Fact]
    public void EmptyScanReturnsNullInsteadOfEmptyString()
    {
        var assembler = new RawInputLineAssembler();

        var result = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);

        Assert.Null(result);
    }

    [Fact]
    public void BufferResetsAfterEachCompletedScan()
    {
        var assembler = new RawInputLineAssembler();

        FeedDigits(assembler, "111");
        var first = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);
        FeedDigits(assembler, "222");
        var second = assembler.Feed(VirtualKeyMap.VkReturn, KeyDown);

        Assert.Equal("111", first);
        Assert.Equal("222", second);
    }
}
