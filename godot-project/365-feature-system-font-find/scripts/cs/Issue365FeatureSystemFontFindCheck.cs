using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue365FeatureSystemFontFind;

/// <summary>
/// C# checks of 365-feature-system-font-find. Check the same things as scripts/gd/check.gd.
/// Installed fonts differ per machine: the checks with named fonts run on Windows (Arial, Courier New and Segoe UI ship
/// with every Windows), the other desktop platforms check the generic "sans-serif" family, the Web expects null.
/// </summary>
public partial class Issue365FeatureSystemFontFindCheck : FeatureCheckCs
{
    private const string Missing = "Truely Missing Font 365";
    private const string Dir = "user://365-feature-system-font-find-cs";

    protected override string CheckName => "365-feature-system-font-find";

    protected override async Task RunChecks()
    {
        CheckMissingFont();
        if (OS.HasFeature("web"))
        {
            Expect(SystemFont.Find("sans-serif") == null, "the Web has no system fonts: Find() returns null");
            Expect(!ResourceLoader.Exists("sysfont://sans-serif"), "the Web has no system fonts: Exists() is false");
            return;
        }
        Expect(SystemFont.Find("sans-serif") != null, "Find(sans-serif) resolves the platform's default font");
        if (OS.GetName() != "Windows")
        {
            return;
        }
        await CheckFind();
        CheckFindFirst();
        CheckLoader();
        CheckTheme();
        CheckPerformance();
    }

    private void CheckMissingFont()
    {
        Expect(SystemFont.Find(Missing) == null, "Find() of a missing family returns null");
        Expect(SystemFont.FindFirst([Missing, ""]) == null, "FindFirst() without an installed family returns null");
        Expect(SystemFont.FindFirst([]) == null, "FindFirst([]) returns null");
        Expect(!ResourceLoader.Exists("sysfont://Truely%20Missing%20Font%20365"), "ResourceLoader.Exists() is false for a missing font");
        Expect(ResourceLoader.Load("sysfont://Truely%20Missing%20Font%20365") == null, "Load() of a missing font returns null");
    }

    private async Task CheckFind()
    {
        SystemFont font = SystemFont.Find("Arial");
        if (!Expect(font != null, "Find(Arial) returns a SystemFont"))
        {
            return;
        }
        Expect(font.FontNames.SequenceEqual(new[] { "Arial" }), "FontNames holds only the requested family");
        ExpectEqual(font.GetFontName(), "Arial", "the resolved face is Arial");
        Expect(font.HasChar('A'), "the found font has the glyph A");
        Expect(SystemFont.Find("arial") != null, "the family name is compared case-insensitively");

        SystemFont boldItalic = SystemFont.Find("Arial", 700, true);
        if (Expect(boldItalic != null, "Find(Arial, 700, true) returns a SystemFont"))
        {
            ExpectEqual(boldItalic.FontWeight, 700, "FontWeight is the requested weight");
            Expect(boldItalic.FontItalic, "FontItalic is set");
            Expect(boldItalic.GetFontStyle().HasFlag(TextServer.FontStyle.Italic), "the resolved face is italic");
        }

        var label = new Label { Text = "Hello, system font!" };
        label.AddThemeFontOverride("font", font);
        AddChild(label);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(label.GetMinimumSize().X > 0.0f, "a Label renders text with the found font");
        label.QueueFree();
    }

    private void CheckFindFirst()
    {
        SystemFont font = SystemFont.FindFirst([Missing, "Segoe UI", "Arial"]);
        if (Expect(font != null, "FindFirst() skips the missing family"))
        {
            Expect(font.FontNames.SequenceEqual(new[] { "Segoe UI" }), "FindFirst() returns the first installed family");
        }
    }

    private void CheckLoader()
    {
        string path = $"sysfont://{Missing.URIEncode()},Courier%20New?weight=700&italic=true";
        Expect(ResourceLoader.Exists("sysfont://Arial"), "ResourceLoader.Exists(sysfont://Arial)");
        Expect(ResourceLoader.Exists("sysfont://Arial", "Font"), "Exists() with type hint Font");
        var font = GD.Load<SystemFont>(path);
        if (!Expect(font != null, $"Load({path}) returns a SystemFont"))
        {
            return;
        }
        Expect(font.FontNames.SequenceEqual(new[] { "Courier New" }), "the first installed family of the path is used");
        ExpectEqual(font.FontWeight, 700, "weight from the query");
        Expect(font.FontItalic, "italic from the query");
        ExpectEqual(font.ResourcePath, path, "ResourcePath is the sysfont:// path");
        Expect(GD.Load<SystemFont>(path) == font, "a second Load() returns the cached font");
        Expect(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) != font, "CacheMode.Ignore loads a new font");
        Expect(ResourceLoader.Load("sysfont://Arial?size=16") == null, "an unknown query option fails");
    }

    private void CheckTheme()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        var theme = new Theme { DefaultFont = GD.Load<Font>("sysfont://Segoe%20UI?weight=600") };
        Expect(ResourceSaver.Save(theme, Dir.PathJoin("theme.tres")) == Error.Ok, "save a Theme that uses a sysfont:// font");
        Expect(FileAccess.GetFileAsString(Dir.PathJoin("theme.tres")).Contains("sysfont://Segoe%20UI?weight=600"), "the Theme file references the sysfont:// path");
        var loaded = ResourceLoader.Load<Theme>(Dir.PathJoin("theme.tres"), null, ResourceLoader.CacheMode.Ignore);
        if (Expect(loaded?.DefaultFont is SystemFont, "the Theme loads with its system font"))
        {
            Expect(((SystemFont)loaded.DefaultFont).FontNames.SequenceEqual(new[] { "Segoe UI" }), "the Theme's font is Segoe UI");
        }
    }

    /// <summary>Prints the timings for the wiki's Measurements section.</summary>
    private void CheckPerformance()
    {
        const int Runs = 20;
        ulong start = Time.GetTicksUsec();
        for (int run = 0; run < Runs; run++)
        {
            SystemFont.Find("Arial");
        }
        double findMs = (Time.GetTicksUsec() - start) / 1000.0 / Runs;
        start = Time.GetTicksUsec();
        for (int run = 0; run < Runs; run++)
        {
            SystemFont.Find(Missing);
        }
        double missingMs = (Time.GetTicksUsec() - start) / 1000.0 / Runs;
        start = Time.GetTicksUsec();
        for (int run = 0; run < Runs; run++)
        {
            ResourceLoader.Load("sysfont://Arial", "", ResourceLoader.CacheMode.Ignore);
        }
        double loadMs = (Time.GetTicksUsec() - start) / 1000.0 / Runs;
        var kept = GD.Load<SystemFont>("sysfont://Arial");
        start = Time.GetTicksUsec();
        for (int run = 0; run < 1000; run++)
        {
            GD.Load<SystemFont>("sysfont://Arial");
        }
        double cachedUs = (Time.GetTicksUsec() - start) / 1000.0;
        GD.Print($"PERF cs: Find(Arial) = {findMs:F3} ms, Find(missing) = {missingMs:F3} ms, Load(sysfont://Arial) uncached = {loadMs:F3} ms, cached = {cachedUs:F2} us");
        Expect(kept != null && findMs < 1000.0, "Find() takes less than 1 s");
    }
}
