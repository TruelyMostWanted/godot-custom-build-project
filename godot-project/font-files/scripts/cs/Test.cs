using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.FontFiles;

/// <summary>
/// Full API test of Font Files (runtime font loaders, FontFile saver, SystemFont.Find, sysfont:// paths, shader files).
/// Same tests as scripts/gd/Test.gd. Test fonts: a subset of JetBrains Mono (SIL OFL, see resources/LICENSE.JetBrainsMono.txt)
/// as .ttf, .otf, .woff and .woff2 and a hand-made BMFont. They use the import mode Keep File, so they stay raw files.
/// Named system fonts are checked on Windows (Arial, Courier New and Segoe UI ship with it); the Web has no system fonts.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://font-files-test";
    private const string Res = "res://font-files/resources";
    private const int BmfontSize = 12;
    private const float BmfontStringWidth = 40.0f;
    private const string Missing = "Truely Missing Font";
    private const string MissingPath = "sysfont://Truely%20Missing%20Font";
    private const int Bold = 700;
    private const int PerfRuns = 20;

    private static readonly string[] FontFiles =
        ["JetBrainsMono_Subset.ttf", "JetBrainsMono_Subset.otf", "JetBrainsMono_Subset.woff", "JetBrainsMono_Subset.woff2"];

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir + "/fonts");
        foreach (string fileName in FontFiles)
        {
            DirAccess.CopyAbsolute($"{Res}/fonts/{fileName}", $"{Dir}/fonts/{fileName}");
        }
        DirAccess.CopyAbsolute(Res + "/bmfont/TestBitmap.fnt", Dir + "/fonts/TestBitmap.fnt");
        DirAccess.CopyAbsolute(Res + "/bmfont/TestBitmap_0.png", Dir + "/fonts/TestBitmap_0.png");
        TestFontFiles();
        TestBmfont();
        TestCache();
        TestSave();
        TestSaveErrors();
        TestImporterPrecedence();
        TestMissingSystemFont();
        if (OS.HasFeature("web"))
        {
            TestWebHasNoSystemFonts();
        }
        else
        {
            TestGenericSystemFont();
        }
        if (OS.GetName() == "Windows")
        {
            TestFind();
            TestFindFirst();
            TestSysfontPath();
            TestSysfontTheme();
            TestFindPerformance();
        }
        TestShaderFiles();
        return Task.CompletedTask;
    }

    private static FontFile LoadFont(string path)
    {
        return ResourceLoader.Load(path, "FontFile", ResourceLoader.CacheMode.Ignore) as FontFile;
    }

    private void TestFontFiles()
    {
        foreach (string fileName in FontFiles)
        {
            string path = $"{Dir}/fonts/{fileName}";
            string extension = fileName.GetExtension();
            Expect(ResourceLoader.Exists(path), $"ResourceLoader.Exists finds the .{extension}");
            Expect(ResourceLoader.Exists(path, "Font"), $"ResourceLoader.Exists finds the .{extension} with the hint Font");
            Expect(Array.IndexOf(ResourceLoader.GetRecognizedExtensionsForType("FontFile"), extension) >= 0, $"{extension} is a recognized FontFile extension");
            FontFile font = LoadFont(path);
            Expect(font != null, $".{extension} loads as FontFile");
            Expect(font.Data.AsSpan().SequenceEqual(FileAccess.GetFileAsBytes(path)), $".{extension}: data holds the file bytes unchanged");
            Expect(font.HasChar('A'), $".{extension} font has the glyph A");
            Vector2 size = font.GetStringSize("Hello", HorizontalAlignment.Left, -1, 16);
            Expect(size.X > 0.0f, $".{extension} font shapes text");
        }
    }

    private void TestBmfont()
    {
        string path = Dir + "/fonts/TestBitmap.fnt";
        Expect(ResourceLoader.Exists(path, "FontFile"), "ResourceLoader.Exists finds the BMFont .fnt");
        FontFile font = LoadFont(path);
        Expect(font != null, "the BMFont loads as FontFile");
        Expect(font.Data.Length == 0, "a BMFont has no font data");
        ExpectEqual(font.FixedSize, BmfontSize, "FixedSize is the size of the info line");
        Expect(font.HasChar('A'), "the BMFont has the glyph A");
        ExpectEqual(font.GetTextureCount(0, new Vector2I(BmfontSize, 0)), 1, "the page texture is loaded from the folder of the .fnt");
        Vector2 size = font.GetStringSize("Hello", HorizontalAlignment.Left, -1, BmfontSize);
        ExpectEqual(size.X, BmfontStringWidth, "BMFont advance: 5 characters of 8 px");
    }

    private void TestCache()
    {
        string path = Dir + "/fonts/JetBrainsMono_Subset.ttf";
        Resource first = GD.Load(path);
        Resource second = GD.Load(path);
        Expect(first == second, "Load caches the font");
        ExpectEqual(first.ResourcePath, path, "ResourcePath is the loaded path");
    }

    private void TestSave()
    {
        foreach (string fileName in FontFiles)
        {
            string source = $"{Dir}/fonts/{fileName}";
            string extension = fileName.GetExtension();
            FontFile font = LoadFont(source);
            string copy = $"{Dir}/copy.{extension}";
            Expect(Array.IndexOf(ResourceSaver.GetRecognizedExtensions(font), extension) >= 0, $"ResourceSaver offers .{extension} for .{extension} data");
            ExpectEqual(ResourceSaver.Save(font, copy), Error.Ok, $"ResourceSaver.Save writes a .{extension} copy");
            Expect(FileAccess.GetFileAsBytes(copy).AsSpan().SequenceEqual(FileAccess.GetFileAsBytes(source)), $"the .{extension} copy has the same bytes");
            Expect(LoadFont(copy).Data.AsSpan().SequenceEqual(font.Data), $"the .{extension} copy loads again");
        }
        FontFile ttf = LoadFont(Dir + "/fonts/JetBrainsMono_Subset.ttf");
        ExpectEqual(ResourceSaver.Save(ttf, Dir + "/ttf_as.otf"), Error.Ok, "sfnt data saves as .otf");
    }

    private void TestSaveErrors()
    {
        var empty = new FontFile();
        Expect(Array.IndexOf(ResourceSaver.GetRecognizedExtensions(empty), "ttf") < 0, "no .ttf is offered for a FontFile without data");
        ExpectEqual(ResourceSaver.Save(empty, Dir + "/empty.ttf"), Error.InvalidData, "a FontFile without data cannot be saved as .ttf");
        Expect(!FileAccess.FileExists(Dir + "/empty.ttf"), "nothing is written for a FontFile without data");
        FontFile bmfont = LoadFont(Dir + "/fonts/TestBitmap.fnt");
        ExpectEqual(ResourceSaver.Save(bmfont, Dir + "/bmfont.ttf"), Error.InvalidData, "a BMFont cannot be saved as .ttf");
        ExpectEqual(ResourceSaver.Save(bmfont, Dir + "/bmfont.tres"), Error.Ok, "a BMFont saves as .tres");
        FontFile ttf = LoadFont(Dir + "/fonts/JetBrainsMono_Subset.ttf");
        Expect(Array.IndexOf(ResourceSaver.GetRecognizedExtensions(ttf), "woff2") < 0, "no .woff2 is offered for TrueType data");
        ExpectEqual(ResourceSaver.Save(ttf, Dir + "/ttf_as.woff2"), Error.InvalidData, "TrueType data is not converted to .woff2");
    }

    // The .import file of the imported WOFF2 sets antialiasing to None: only the importer applies it.
    private void TestImporterPrecedence()
    {
        var imported = GD.Load<FontFile>(Res + "/imported/JetBrainsMono_Imported.woff2");
        ExpectEqual(imported.Antialiasing, TextServer.FontAntialiasing.None, "an imported font comes from the importer (antialiasing None)");
        string keptPath = Res + "/fonts/JetBrainsMono_Subset.ttf";
        FontFile kept = LoadFont(keptPath);
        ExpectEqual(kept.Antialiasing, TextServer.FontAntialiasing.Gray, "a Keep File font comes from the runtime loader (default antialiasing)");
        Expect(kept.Data.AsSpan().SequenceEqual(FileAccess.GetFileAsBytes(keptPath)), "the Keep File font data is the raw file");
    }

    private void TestMissingSystemFont()
    {
        Expect(SystemFont.Find(Missing) == null, "Find of a missing family returns null");
        Expect(SystemFont.FindFirst([Missing, ""]) == null, "FindFirst without an installed family returns null");
        Expect(SystemFont.FindFirst([]) == null, "FindFirst of an empty list returns null");
        Expect(!ResourceLoader.Exists(MissingPath), "ResourceLoader.Exists is false for a missing sysfont:// font");
        Expect(ResourceLoader.Load(MissingPath) == null, "Load of a missing sysfont:// font returns null");
    }

    private void TestWebHasNoSystemFonts()
    {
        Expect(SystemFont.Find("sans-serif") == null, "the Web has no system fonts: Find returns null");
        Expect(!ResourceLoader.Exists("sysfont://sans-serif"), "the Web has no system fonts: Exists is false");
    }

    private void TestGenericSystemFont()
    {
        Expect(SystemFont.Find("sans-serif") != null, "Find(sans-serif) resolves the platform's default font");
    }

    private void TestFind()
    {
        SystemFont font = SystemFont.Find("Arial");
        Expect(font != null, "Find(Arial) returns a SystemFont");
        Expect(font.FontNames.AsSpan().SequenceEqual(["Arial"]), "FontNames holds only the requested family");
        ExpectEqual(font.GetFontName(), "Arial", "the resolved face is Arial");
        Expect(font.HasChar('A'), "the found font has the glyph A");
        Expect(SystemFont.Find("arial") != null, "the family name is compared case-insensitively");
        SystemFont boldItalic = SystemFont.Find("Arial", Bold, true);
        ExpectEqual(boldItalic.FontWeight, Bold, "FontWeight is the requested weight");
        Expect(boldItalic.FontItalic, "FontItalic is set");
        Expect((boldItalic.GetFontStyle() & TextServer.FontStyle.Italic) != 0, "the resolved face is italic");
    }

    private void TestFindFirst()
    {
        SystemFont font = SystemFont.FindFirst([Missing, "Segoe UI", "Arial"]);
        Expect(font.FontNames.AsSpan().SequenceEqual(["Segoe UI"]), "FindFirst returns the first installed family");
    }

    private void TestSysfontPath()
    {
        string path = $"sysfont://{Missing.URIEncode()},Courier%20New?weight=700&italic=true";
        Expect(ResourceLoader.Exists("sysfont://Arial"), "ResourceLoader.Exists finds sysfont://Arial");
        Expect(ResourceLoader.Exists("sysfont://Arial", "Font"), "ResourceLoader.Exists finds sysfont://Arial with the hint Font");
        var font = GD.Load<SystemFont>(path);
        Expect(font.FontNames.AsSpan().SequenceEqual(["Courier New"]), "the first installed family of the path is used");
        ExpectEqual(font.FontWeight, Bold, "weight comes from the query");
        Expect(font.FontItalic, "italic comes from the query");
        ExpectEqual(font.ResourcePath, path, "ResourcePath is the sysfont:// path");
        Expect(GD.Load(path) == font, "a second Load returns the cached font");
        Expect(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) != font, "CacheMode.Ignore loads a new font");
        Expect(GD.Load("sysfont://Arial?size=16") == null, "an unknown query option fails");
    }

    private void TestSysfontTheme()
    {
        string themePath = Dir + "/theme.tres";
        var theme = new Theme { DefaultFont = GD.Load<Font>("sysfont://Segoe%20UI?weight=600") };
        ExpectEqual(ResourceSaver.Save(theme, themePath), Error.Ok, "a Theme with a sysfont:// font saves");
        Expect(FileAccess.GetFileAsString(themePath).Contains("sysfont://Segoe%20UI?weight=600"), "the Theme file references the sysfont:// path");
        var loaded = ResourceLoader.Load(themePath, "", ResourceLoader.CacheMode.Ignore) as Theme;
        var font = loaded.DefaultFont as SystemFont;
        Expect(font.FontNames.AsSpan().SequenceEqual(["Segoe UI"]), "the loaded Theme finds its system font again");
    }

    // Prints the timings for the wiki's Measurements section.
    private void TestFindPerformance()
    {
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < PerfRuns; i++)
        {
            SystemFont.Find("Arial");
        }
        double findMs = timer.Elapsed.TotalMilliseconds / PerfRuns;
        timer.Restart();
        for (int i = 0; i < PerfRuns; i++)
        {
            SystemFont.Find(Missing);
        }
        double missingMs = timer.Elapsed.TotalMilliseconds / PerfRuns;
        timer.Restart();
        for (int i = 0; i < PerfRuns; i++)
        {
            ResourceLoader.Load("sysfont://Arial", "", ResourceLoader.CacheMode.Ignore);
        }
        double loadMs = timer.Elapsed.TotalMilliseconds / PerfRuns;
        GD.Print($"PERF cs: find(Arial) = {findMs:F3} ms, find(missing) = {missingMs:F3} ms, load(sysfont://Arial) uncached = {loadMs:F3} ms");
        Expect(findMs < 1000.0, "Find takes less than 1 s");
    }

    private void TestShaderFiles()
    {
        string includePath = Dir + "/tint.gdshaderinc";
        string shaderPath = Dir + "/tinted.gdshader";
        var include = new ShaderInclude { Code = "uniform vec4 tint : source_color = vec4(1.0);\n" };
        ExpectEqual(ResourceSaver.Save(include, includePath), Error.Ok, "ResourceSaver.Save writes a .gdshaderinc to user://");
        var loadedInclude = ResourceLoader.Load(includePath, "", ResourceLoader.CacheMode.Ignore) as ShaderInclude;
        ExpectEqual(loadedInclude.Code, include.Code, "the ShaderInclude code survives save and load");
        var shader = new Shader { Code = $"shader_type canvas_item;\n#include \"{includePath}\"\nvoid fragment() {{ COLOR *= tint; }}\n" };
        ExpectEqual(ResourceSaver.Save(shader, shaderPath), Error.Ok, "ResourceSaver.Save writes a .gdshader to user://");
        var loadedShader = ResourceLoader.Load(shaderPath, "", ResourceLoader.CacheMode.Ignore) as Shader;
        ExpectEqual(loadedShader.Code, shader.Code, "the Shader code survives save and load");
        ExpectEqual(loadedShader.GetMode(), Shader.Mode.CanvasItem, "the shader mode comes from the loaded code");
    }
}
