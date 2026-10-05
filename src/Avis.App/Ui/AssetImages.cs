namespace Avis.App.Ui;

/// <summary>Optional images from the Assets folder next to the exe. A missing image is never an error - callers fall back to text.</summary>
internal static class AssetImages
{
    public static Image? TryLoad(params string[] fileNames)
    {
        foreach (var name in fileNames)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", name);
                if (File.Exists(path))
                {
                    // Load via a copy so the file isn't locked for the life of the app.
                    using var stream = new MemoryStream(File.ReadAllBytes(path));
                    return Image.FromStream(stream);
                }
            }
            catch
            {
                // decorative only
            }
        }
        return null;
    }
}
