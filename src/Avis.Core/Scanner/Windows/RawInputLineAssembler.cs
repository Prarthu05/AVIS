using System.Text;

namespace Avis.Scanner.Windows;

/// <summary>
/// Turns a stream of (virtualKey, isKeyUp) key events into decoded scan strings.
/// Pure logic, no Win32/WinForms dependency - Feed() returns the assembled string
/// once an Enter keypress is seen (matching the scanner's confirmed Enter-terminated
/// output), or null while a scan is still in progress.
/// </summary>
public class RawInputLineAssembler
{
    private readonly StringBuilder _buffer = new();
    private bool _shiftHeld;

    public string? Feed(int virtualKey, bool isKeyUp)
    {
        if (VirtualKeyMap.IsShiftKey(virtualKey))
        {
            _shiftHeld = !isKeyUp;
            return null;
        }

        if (isKeyUp)
        {
            return null;
        }

        if (VirtualKeyMap.IsEnterKey(virtualKey))
        {
            var text = _buffer.ToString().Trim();
            _buffer.Clear();
            return text.Length > 0 ? text : null;
        }

        var ch = VirtualKeyMap.ToChar(virtualKey, _shiftHeld);
        if (ch is not null)
        {
            _buffer.Append(ch.Value);
        }
        return null;
    }
}
