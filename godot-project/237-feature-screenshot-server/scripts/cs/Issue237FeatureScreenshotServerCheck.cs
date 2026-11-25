using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue237FeatureScreenshotServer;

/// <summary>C# checks of 237-feature-screenshot-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue237FeatureScreenshotServerCheck : FeatureCheckCs
{
    private const string Folder = "user://237-feature-screenshot-server/cs";

    private readonly List<string> _saved = new();

    protected override string CheckName => "237-feature-screenshot-server";

    protected override Task RunChecks()
    {
        Expect(Engine.HasSingleton("ScreenshotServer"), "ScreenshotServer is a singleton");
        ExpectEqual((string)ProjectSettings.GetSetting("display/screenshots/folder"), "user://screenshots", "default folder");
        ExpectEqual((string)ProjectSettings.GetSetting("display/screenshots/format"), "png", "default format");

        Image image = Image.CreateEmpty(32, 16, false, Image.Format.Rgba8);
        image.Fill(Colors.CornflowerBlue);
        ScreenshotServer.ScreenshotSaved += OnSaved;

        foreach (string extension in new[] { "png", "jpg", "webp" })
        {
            string path = Folder.PathJoin("explicit." + extension);
            ExpectEqual(ScreenshotServer.SaveScreenshot(image, path), path, $"save_screenshot() writes .{extension}");
            Image loaded = Image.LoadFromFile(path);
            Expect(loaded != null && loaded.GetSize() == new Vector2I(32, 16), $".{extension} loads again");
        }

        ProjectSettings.SetSetting("display/screenshots/folder", Folder.PathJoin("auto"));
        ProjectSettings.SetSetting("display/screenshots/file_name", "{project} check");
        ProjectSettings.SetSetting("display/screenshots/format", "jpg");
        string next = ScreenshotServer.MakeScreenshotPath();
        Expect(next.StartsWith(Folder.PathJoin("auto")) && next.EndsWith(".jpg"), "make_screenshot_path() uses folder and format");
        string first = ScreenshotServer.SaveScreenshot(image);
        string second = ScreenshotServer.SaveScreenshot(image);
        Expect(first != "" && second != "" && first != second, "two screenshots with the same name don't overwrite each other");
        Expect(second.EndsWith(" (2).jpg"), "the second one gets (2)");
        Expect(first.GetFile().StartsWith((string)ProjectSettings.GetSetting("application/config/name")), "{project} is the project name");
        ExpectEqual(_saved.Count, 5, "screenshot_saved was emitted for every saved file");

        ProjectSettings.SetSetting("display/screenshots/folder", "user://screenshots");
        ProjectSettings.SetSetting("display/screenshots/file_name", "Screenshot {year}-{month}-{day} {hour}-{minute}-{second}");
        ProjectSettings.SetSetting("display/screenshots/format", "png");
        ScreenshotServer.ScreenshotSaved -= OnSaved;
        foreach (string path in _saved)
        {
            DirAccess.RemoveAbsolute(path);
        }
        return Task.CompletedTask;
    }

    private void OnSaved(string path, Image image)
    {
        if (image != null)
        {
            _saved.Add(path);
        }
    }
}
