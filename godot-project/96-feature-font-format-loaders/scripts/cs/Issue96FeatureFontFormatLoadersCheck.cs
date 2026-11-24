using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue96FeatureFontFormatLoaders;

/// <summary>
/// C# checks of 96-feature-font-format-loaders. Check the same things as scripts/gd/check.gd.
/// Test fonts: an ASCII subset of JetBrains Mono (SIL Open Font License 1.1, see resources/LICENSE.JetBrainsMono.txt)
/// as .ttf, .otf (CFF outlines), .woff and .woff2, and a hand-made BMFont (resources/bmfont).
/// </summary>
public partial class Issue96FeatureFontFormatLoadersCheck : FeatureCheckCs
{
    private const string Dir = "user://96-feature-font-format-loaders-cs";
    private const string Res = "res://96-feature-font-format-loaders/resources";
    private static readonly string[] DynamicFonts = ["JetBrainsMono_Subset.ttf", "JetBrainsMono_Subset.otf", "JetBrainsMono_Subset.woff", "JetBrainsMono_Subset.woff2"];
    private static readonly string[] BmFontFiles = ["TestBitmap.fnt", "TestBitmap_0.png"];

    protected override string CheckName => "96-feature-font-format-loaders";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir.PathJoin("fonts"));
        foreach (string file in DynamicFonts)
        {
            Copy(Res.PathJoin("fonts").PathJoin(file), Dir.PathJoin("fonts").PathJoin(file));
        }
        foreach (string file in BmFontFiles)
        {
            Copy(Res.PathJoin("bmfont").PathJoin(file), Dir.PathJoin("fonts").PathJoin(file));
        }

        foreach (string file in DynamicFonts)
        {
            await CheckDynamicFontFromUser(file);
        }
        await CheckBmFontFromUser();
        CheckCache();
        CheckSaveSameBytes();
        CheckSaveErrors();
        CheckImporterPrecedence();
        CheckShaderFiles();
    }

    private void Copy(string from, string to)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(from);
        Expect(bytes.Length > 0, $"read {from}");
        FileAccess file = FileAccess.Open(to, FileAccess.ModeFlags.Write);
        if (Expect(file != null, $"open {to} for writing"))
        {
            file.StoreBuffer(bytes);
            file.Close();
        }
    }

    /// <summary>Shows the font in a Label and returns the Label's minimum width (> 0 when the font shapes the text).</summary>
    private async Task<float> LabelWidth(Font font)
    {
        var label = new Label { Text = "Hello, font!" };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", 16);
        AddChild(label);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        float width = label.GetMinimumSize().X;
        label.QueueFree();
        return width;
    }

    private async Task CheckDynamicFontFromUser(string file)
    {
        string path = Dir.PathJoin("fonts").PathJoin(file);
        string ext = file.GetExtension();
        Expect(ResourceLoader.Exists(path), $"ResourceLoader.Exists() finds a .{ext} in user://");
        Expect(ResourceLoader.Exists(path, "Font"), $"Exists() with type hint Font (.{ext})");
        Expect(ResourceLoader.GetRecognizedExtensionsForType("FontFile").Contains(ext), $"{ext} is a recognized FontFile extension");

        var font = ResourceLoader.Load<FontFile>(path, null, ResourceLoader.CacheMode.Ignore);
        if (!Expect(font != null, $"ResourceLoader.Load() returns a FontFile from user:// (.{ext})"))
        {
            return;
        }
        Expect(font.Data.SequenceEqual(FileAccess.GetFileAsBytes(path)), $"FontFile.Data holds the file bytes unchanged (.{ext})");
        Expect(font.HasChar('A'), $"the .{ext} font has the glyph A");
        Expect(font.GetStringSize("Hello", HorizontalAlignment.Left, -1, 16).X > 0.0f, $"GetStringSize() > 0 (.{ext})");
        Expect(await LabelWidth(font) > 0.0f, $"a Label renders text with the .{ext} font");
    }

    private async Task CheckBmFontFromUser()
    {
        string path = Dir.PathJoin("fonts/TestBitmap.fnt");
        Expect(ResourceLoader.Exists(path, "FontFile"), "ResourceLoader.Exists() finds a BMFont .fnt in user://");
        var font = ResourceLoader.Load<FontFile>(path, null, ResourceLoader.CacheMode.Ignore);
        if (!Expect(font != null, "ResourceLoader.Load() returns a FontFile from a BMFont in user://"))
        {
            return;
        }
        Expect(font.Data.Length == 0, "a BMFont has no font data");
        ExpectEqual(font.FixedSize, 12, "BMFont size from the info line");
        Expect(font.HasChar('A'), "the BMFont has the glyph A");
        ExpectEqual(font.GetTextureCount(0, new Vector2I(12, 0)), 1, "the BMFont page texture is loaded from the folder of the .fnt");
        Expect(Mathf.IsEqualApprox(font.GetStringSize("Hello", HorizontalAlignment.Left, -1, 12).X, 40.0f), "BMFont advance: 5 chars x 8 px");
        Expect(await LabelWidth(font) > 0.0f, "a Label renders text with the BMFont");
    }

    private void CheckCache()
    {
        string path = Dir.PathJoin("fonts/JetBrainsMono_Subset.ttf");
        var first = ResourceLoader.Load(path);
        var second = ResourceLoader.Load(path);
        Expect(first != null && first == second, "Load() caches the font");
        ExpectEqual(first?.ResourcePath ?? "", path, "ResourcePath of the cached font");
    }

    private void CheckSaveSameBytes()
    {
        foreach (string file in DynamicFonts)
        {
            string source = Dir.PathJoin("fonts").PathJoin(file);
            string ext = file.GetExtension();
            var font = ResourceLoader.Load<FontFile>(source, "FontFile", ResourceLoader.CacheMode.Ignore);
            if (!Expect(font != null, $"load the .{ext} font to save it"))
            {
                continue;
            }
            string copy = Dir.PathJoin("copy." + ext);
            Expect(ResourceSaver.GetRecognizedExtensions(font).Contains(ext), $"ResourceSaver offers .{ext} for .{ext} data");
            ExpectEqual(ResourceSaver.Save(font, copy), Error.Ok, $"ResourceSaver.Save() writes a .{ext} copy");
            Expect(FileAccess.GetFileAsBytes(copy).SequenceEqual(FileAccess.GetFileAsBytes(source)), $"the .{ext} copy has the same bytes as the source");
            var reloaded = ResourceLoader.Load<FontFile>(copy, "FontFile", ResourceLoader.CacheMode.Ignore);
            Expect(reloaded != null && reloaded.Data.SequenceEqual(font.Data), $"the .{ext} copy loads again");
        }
        // TrueType data may also be stored as .otf (both are sfnt).
        var ttf = ResourceLoader.Load<FontFile>(Dir.PathJoin("fonts/JetBrainsMono_Subset.ttf"), "FontFile", ResourceLoader.CacheMode.Ignore);
        if (ttf != null)
        {
            ExpectEqual(ResourceSaver.Save(ttf, Dir.PathJoin("ttf_as.otf")), Error.Ok, "sfnt data saves as .otf");
        }
    }

    private void CheckSaveErrors()
    {
        string emptyPath = Dir.PathJoin("empty.ttf");
        if (FileAccess.FileExists(emptyPath))
        {
            DirAccess.RemoveAbsolute(emptyPath);
        }
        var empty = new FontFile();
        Expect(!ResourceSaver.GetRecognizedExtensions(empty).Contains("ttf"), "no .ttf offered for a FontFile without data");
        ExpectEqual(ResourceSaver.Save(empty, emptyPath), Error.InvalidData, "a FontFile without data cannot be saved as .ttf");
        Expect(!FileAccess.FileExists(emptyPath), "nothing is written for a FontFile without data");

        var bmFont = ResourceLoader.Load<FontFile>(Dir.PathJoin("fonts/TestBitmap.fnt"), "FontFile", ResourceLoader.CacheMode.Ignore);
        if (bmFont != null)
        {
            ExpectEqual(ResourceSaver.Save(bmFont, Dir.PathJoin("bmfont.ttf")), Error.InvalidData, "a BMFont cannot be saved as .ttf");
            ExpectEqual(ResourceSaver.Save(bmFont, Dir.PathJoin("bmfont.tres")), Error.Ok, "a BMFont still saves as .tres");
        }

        var ttf = ResourceLoader.Load<FontFile>(Dir.PathJoin("fonts/JetBrainsMono_Subset.ttf"), "FontFile", ResourceLoader.CacheMode.Ignore);
        if (ttf != null)
        {
            Expect(!ResourceSaver.GetRecognizedExtensions(ttf).Contains("woff2"), "no .woff2 offered for TrueType data");
            ExpectEqual(ResourceSaver.Save(ttf, Dir.PathJoin("ttf_as.woff2")), Error.InvalidData, "TrueType data is not converted to .woff2");
        }
    }

    private void CheckImporterPrecedence()
    {
        // The .import file of the imported WOFF2 sets antialiasing=0: only the importer applies it.
        var imported = ResourceLoader.Load<FontFile>(Res.PathJoin("imported/JetBrainsMono_Imported.woff2"));
        if (Expect(imported != null, "imported res:// font loads"))
        {
            ExpectEqual(imported.Antialiasing, TextServer.FontAntialiasing.None, "imported res:// font comes from the importer (antialiasing None)");
        }
        // The raw fonts use the import mode "Keep File": the runtime loader reads them from res:// directly.
        string keptPath = Res.PathJoin("fonts/JetBrainsMono_Subset.ttf");
        var kept = ResourceLoader.Load<FontFile>(keptPath, null, ResourceLoader.CacheMode.Ignore);
        if (Expect(kept != null, "font with import mode Keep File loads from res://"))
        {
            ExpectEqual(kept.Antialiasing, TextServer.FontAntialiasing.Gray, "Keep File font comes from the runtime loader (default antialiasing)");
            Expect(kept.Data.SequenceEqual(FileAccess.GetFileAsBytes(keptPath)), "Keep File font data is the raw file");
        }
    }

    private void CheckShaderFiles()
    {
        string includePath = Dir.PathJoin("tint.gdshaderinc");
        string shaderPath = Dir.PathJoin("tinted.gdshader");

        var include = new ShaderInclude { Code = "uniform vec4 tint : source_color = vec4(1.0);\n" };
        ExpectEqual(ResourceSaver.Save(include, includePath), Error.Ok, "ResourceSaver.Save() writes a .gdshaderinc to user://");
        var loadedInclude = ResourceLoader.Load<ShaderInclude>(includePath, null, ResourceLoader.CacheMode.Ignore);
        if (Expect(loadedInclude != null, "ResourceLoader.Load() returns a ShaderInclude from user://"))
        {
            ExpectEqual(loadedInclude.Code, include.Code, "ShaderInclude code after save and load");
        }

        var shader = new Shader { Code = $"shader_type canvas_item;\n#include \"{includePath}\"\n\nvoid fragment() {{\n\tCOLOR *= tint;\n}}\n" };
        ExpectEqual(ResourceSaver.Save(shader, shaderPath), Error.Ok, "ResourceSaver.Save() writes a .gdshader to user://");
        var loadedShader = ResourceLoader.Load<Shader>(shaderPath, null, ResourceLoader.CacheMode.Ignore);
        if (Expect(loadedShader != null, "ResourceLoader.Load() returns a Shader from user://"))
        {
            ExpectEqual(loadedShader.Code, shader.Code, "Shader code after save and load");
            ExpectEqual(loadedShader.GetMode(), Shader.Mode.CanvasItem, "shader mode from the loaded code");
        }
    }
}
