using Godot;

namespace TruelyInteractive.FontFiles;

/// <summary>Minimal example of Font Files: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>Not run here: the settings menu snippet (it needs a UI with a check box).</remarks>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Setup: fonts in user://fonts (the guide assumes them) and Labels named Label, Title and Code.
        DirAccess.MakeDirRecursiveAbsolute("user://fonts");
        DirAccess.CopyAbsolute("res://font-files/resources/fonts/JetBrainsMono_Subset.ttf", "user://fonts/MyFont.ttf");
        DirAccess.CopyAbsolute("res://font-files/resources/bmfont/TestBitmap.fnt", "user://fonts/Pixel.fnt");
        DirAccess.CopyAbsolute("res://font-files/resources/bmfont/TestBitmap_0.png", "user://fonts/TestBitmap_0.png");
        foreach (string labelName in new[] { "Label", "Title", "Code" })
        {
            AddChild(new Label { Name = labelName });
        }

        // Use a font the player added
        var font = GD.Load<FontFile>("user://fonts/MyFont.ttf");
        var label = GetNode<Label>("Label");
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", 24);
        GD.Print("Font data bytes: ", font.Data.Length);

        // Load a bitmap font (the page of Pixel.fnt is named TestBitmap_0.png, so the sample .fnt is copied as Pixel.fnt)
        var pixelFont = GD.Load<FontFile>("user://fonts/Pixel.fnt");
        GD.Print(pixelFont.FixedSize);

        // Find an installed font
        SystemFont systemFont = SystemFont.FindFirst(["Segoe UI", "SF Pro Text", "Roboto", "Noto Sans"], 600);
        if (systemFont != null)
        {
            GetNode<Label>("Title").AddThemeFontOverride("font", systemFont);
        }

        SystemFont codeFont = SystemFont.Find("Consolas");
        GD.Print(codeFont != null); // false if Consolas is not installed

        GD.Print("Segoe UI installed: ", SystemFont.Find("Segoe UI") != null);
        GD.Print("NoSuchFont installed: ", SystemFont.Find("NoSuchFont") != null);
        SystemFont first = SystemFont.FindFirst(["NoSuchFont", "Segoe UI"], 600);
        GD.Print("find_first picks: ", first.FontNames[0], " weight ", first.FontWeight);

        // sysfont:// paths
        if (ResourceLoader.Exists("sysfont://Consolas"))
        {
            GetNode<Label>("Code").AddThemeFontOverride("font", GD.Load<Font>("sysfont://Consolas"));
        }

        var theme = new Theme { DefaultFont = GD.Load<Font>("sysfont://Segoe%20UI,Noto%20Sans?weight=600") };
        ResourceSaver.Save(theme, "user://ui_theme.tres");
        GD.Print("Theme saved with font: ", theme.DefaultFont.ResourcePath);

        // Load a shader from user://
        DirAccess.MakeDirRecursiveAbsolute("user://shaders");
        var include = new ShaderInclude { Code = "uniform vec4 tint : source_color = vec4(1.0);\n" };
        ResourceSaver.Save(include, "user://shaders/tint.gdshaderinc");

        var shader = new Shader { Code = "shader_type canvas_item;\n#include \"user://shaders/tint.gdshaderinc\"\nvoid fragment() { COLOR *= tint; }\n" };
        ResourceSaver.Save(shader, "user://shaders/tinted.gdshader");

        var material = new ShaderMaterial { Shader = GD.Load<Shader>("user://shaders/tinted.gdshader") };
        GD.Print("Shader mode: ", material.Shader.GetMode());
        GetTree().Quit();
    }
}
