using Godot;

namespace TruelyInteractive.Screenshots;

/// <summary>Minimal example of Screenshots: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Project settings.
        var folder = (string)ProjectSettings.GetSetting("display/screenshots/folder");
        var format = (string)ProjectSettings.GetSetting("display/screenshots/format");
        GD.Print("Folder: ", folder, ", format: ", format);

        // Taking a screenshot (a viewport has no image in headless runs).
        if (DisplayServer.GetName() != "headless")
        {
            string path = ScreenshotServer.TakeScreenshot();
            GD.Print("Saved ", ProjectSettings.GlobalizePath(path));
        }

        // Capturing without saving.
        ScreenshotServer.ScreenshotSaved += (savedPath, _) => GD.Print("screenshot_saved: ", savedPath);
        Image minimap = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        minimap.Fill(Colors.CornflowerBlue);
        ScreenshotServer.SaveScreenshot(minimap, "user://maps/minimap.webp");
        GetTree().Quit();
    }
}
