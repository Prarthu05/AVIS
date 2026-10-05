namespace Avis.Scanner.Windows;

/// <summary>
/// Windows Virtual-Key code -> character mapping, enough for alphanumeric badge IDs.
/// Not an exhaustive keyboard layout (no function keys, no international layouts) -
/// badge/asset ID barcodes don't need more than this. Pure data/logic, no Win32
/// dependency, so it's usable (and testable) from the platform-agnostic Core project.
/// </summary>
public static class VirtualKeyMap
{
    public const int VkShift = 0x10;
    public const int VkLShift = 0xA0;
    public const int VkRShift = 0xA1;
    public const int VkReturn = 0x0D;
    public const int VkSeparator = 0x6C; // some layouts report numpad Enter as this

    public static bool IsShiftKey(int vkey) => vkey is VkShift or VkLShift or VkRShift;

    public static bool IsEnterKey(int vkey) => vkey is VkReturn or VkSeparator;

    public static char? ToChar(int vkey, bool shiftHeld)
    {
        // VK_0..VK_9 (0x30-0x39) are numerically equal to ASCII '0'-'9' by design.
        if (vkey is >= 0x30 and <= 0x39)
        {
            return shiftHeld ? ShiftedDigit((char)vkey) : (char)vkey;
        }

        // VK_NUMPAD0..VK_NUMPAD9 (0x60-0x69)
        if (vkey is >= 0x60 and <= 0x69)
        {
            return (char)('0' + (vkey - 0x60));
        }

        // VK_A..VK_Z (0x41-0x5A) are numerically equal to ASCII 'A'-'Z' by design.
        if (vkey is >= 0x41 and <= 0x5A)
        {
            return shiftHeld ? (char)vkey : char.ToLowerInvariant((char)vkey);
        }

        return vkey switch
        {
            0xBD => shiftHeld ? '_' : '-',  // VK_OEM_MINUS
            0xBB => shiftHeld ? '+' : '=',  // VK_OEM_PLUS
            0xBA => shiftHeld ? ':' : ';',  // VK_OEM_1
            0xDE => shiftHeld ? '"' : '\'', // VK_OEM_7
            0xDB => shiftHeld ? '{' : '[',  // VK_OEM_4
            0xDD => shiftHeld ? '}' : ']',  // VK_OEM_6
            0xDC => shiftHeld ? '|' : '\\', // VK_OEM_5
            0xBC => shiftHeld ? '<' : ',',  // VK_OEM_COMMA
            0xBE => shiftHeld ? '>' : '.',  // VK_OEM_PERIOD
            0xBF => shiftHeld ? '?' : '/',  // VK_OEM_2
            0xC0 => shiftHeld ? '~' : '`',  // VK_OEM_3
            0x20 => ' ',                    // VK_SPACE
            0x6F => '/',                    // VK_DIVIDE
            0x6A => '*',                    // VK_MULTIPLY
            0x6D => '-',                    // VK_SUBTRACT
            0x6B => '+',                    // VK_ADD
            0x6E => '.',                    // VK_DECIMAL
            _ => null,
        };
    }

    private static char ShiftedDigit(char digit) => digit switch
    {
        '0' => ')', '1' => '!', '2' => '@', '3' => '#', '4' => '$',
        '5' => '%', '6' => '^', '7' => '&', '8' => '*', '9' => '(',
        _ => digit,
    };
}
