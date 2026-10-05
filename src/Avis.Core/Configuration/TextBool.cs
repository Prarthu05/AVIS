namespace Avis.Configuration;

/// <summary>
/// Lenient boolean parsing, matching the values LightGuide's Text2Bool treats as
/// true/false, so INI flags and LightGuide boolean variables read the same way.
/// </summary>
public static class TextBool
{
    private static readonly string[] TrueValues = { "true", "t", "1", "yes", "y", "on" };
    private static readonly string[] FalseValues = { "false", "f", "0", "no", "n", "off" };

    public static bool TryParse(string? value, out bool result)
    {
        var v = value?.Trim().Trim('"').ToLowerInvariant() ?? "";
        if (TrueValues.Contains(v))
        {
            result = true;
            return true;
        }
        if (FalseValues.Contains(v))
        {
            result = false;
            return true;
        }
        result = false;
        return false;
    }

    /// <summary>True only for a recognised true value; anything else (including blank) is false.</summary>
    public static bool IsTrue(string? value) => TryParse(value, out var b) && b;
}
