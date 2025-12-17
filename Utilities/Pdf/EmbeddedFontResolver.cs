//Utilities/Pdf/EmbeddedFontResolver.cs
using System.Reflection;
using PdfSharpCore.Fonts;
using SixLabors.Fonts;

namespace SalesMetrics.Utilities.Pdf;

// Minimal resolver: loads 2 TTFs from disk so PdfSharpCore can embed fonts cross-platform
public sealed class EmbeddedFontResolver : IFontResolver
{
    private readonly string _fontsDir;
    private readonly byte[] _regularBytes;
    private readonly byte[] _boldBytes;

    public EmbeddedFontResolver()
    {
        // resolve to Content/Fonts relative to app base
        _fontsDir = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts");
        _regularBytes = File.ReadAllBytes(Path.Combine(_fontsDir, "Roboto-Regular.ttf"));
        _boldBytes = File.ReadAllBytes(Path.Combine(_fontsDir, "Roboto-Bold.ttf"));
    }

    public string DefaultFontName => "Roboto";

    public byte[] GetFont(string faceName)
    {
        // faceName == "Roboto#" + style suffix we return below
        if (faceName.EndsWith("#B", StringComparison.OrdinalIgnoreCase)) return _boldBytes;
        return _regularBytes;
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // map any request to Roboto regular/bold (no italic in this minimal sample)
        if (string.Equals(familyName, "Roboto", StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(isBold ? "Roboto#B" : "Roboto#R");

        // fallback: still return Roboto
        return new FontResolverInfo(isBold ? "Roboto#B" : "Roboto#R");
    }
}
