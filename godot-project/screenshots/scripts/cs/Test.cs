using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Screenshots;

/// <summary>Full API test of Screenshots (ScreenshotServer): every method, property setting and signal. Same tests as scripts/gd/Test.gd.
/// Runs headless: viewports and screens give nothing, so the capture methods are tested for that answer (they print an ERROR).</summary>
public partial class Test : FeatureTest
{
    private const string Folder = "user://screenshots-test";
    private const int Size = 64;
    private const string DefaultFileName = "Screenshot {year}-{month}-{day} {hour}-{minute}-{second}";
    private const double DefaultQuality = 0.9;

    private readonly List<string> _saved = [];
    private Image _image;

    protected override Task RunTests()
    {
        var noise = new FastNoiseLite();
        _image = noise.GetImage(Size, Size);
        ScreenshotServer.ScreenshotSaved += OnScreenshotSaved;
        TestSingletonAndSettings();
        TestSaveScreenshotFormats();
        TestSaveScreenshotQuality();
        TestSaveScreenshotFailures();
        TestMakeScreenshotPath();
        TestFileNamePlaceholders();
        TestUniqueNames();
        TestScreenshotSavedSignal();
        TestCaptureInHeadless();
        ScreenshotServer.ScreenshotSaved -= OnScreenshotSaved;
        RemoveSavedFiles();
        return Task.CompletedTask;
    }

    private void TestSingletonAndSettings()
    {
        Expect(Engine.HasSingleton("ScreenshotServer"), "ScreenshotServer is a singleton");
        ExpectEqual((string)ProjectSettings.GetSetting("display/screenshots/folder"), "user://screenshots", "default folder");
        ExpectEqual((string)ProjectSettings.GetSetting("display/screenshots/file_name"), DefaultFileName, "default file name");
        ExpectEqual((string)ProjectSettings.GetSetting("display/screenshots/format"), "png", "default format");
        ExpectEqual((double)ProjectSettings.GetSetting("display/screenshots/quality"), DefaultQuality, "default quality");
    }

    private void TestSaveScreenshotFormats()
    {
        foreach (string extension in new[] { "png", "jpg", "webp" })
        {
            string path = Folder.PathJoin("explicit." + extension);
            string saved = ScreenshotServer.SaveScreenshot(_image, path);
            ExpectEqual(saved, path, $"SaveScreenshot writes .{extension} and returns the path");
            Image loaded = Image.LoadFromFile(path);
            ExpectEqual(loaded.GetSize(), new Vector2I(Size, Size), $".{extension} loads again with the same size");
        }
    }

    private void TestSaveScreenshotQuality()
    {
        ProjectSettings.SetSetting("display/screenshots/quality", 0.1);
        string low = ScreenshotServer.SaveScreenshot(_image, Folder.PathJoin("low.jpg"));
        ProjectSettings.SetSetting("display/screenshots/quality", 1.0);
        string high = ScreenshotServer.SaveScreenshot(_image, Folder.PathJoin("high.jpg"));
        ProjectSettings.SetSetting("display/screenshots/quality", DefaultQuality);
        long lowSize = FileAccess.GetFileAsBytes(low).Length;
        long highSize = FileAccess.GetFileAsBytes(high).Length;
        Expect(lowSize < highSize, "a lower quality makes a smaller .jpg");
    }

    private void TestSaveScreenshotFailures()
    {
        string noImage = ScreenshotServer.SaveScreenshot(null, Folder.PathJoin("none.png"));
        ExpectEqual(noImage, "", "SaveScreenshot without an image returns an empty string");
        string badExtension = ScreenshotServer.SaveScreenshot(_image, Folder.PathJoin("bad.bmp"));
        ExpectEqual(badExtension, "", "SaveScreenshot with an unsupported extension returns an empty string");
    }

    private void TestMakeScreenshotPath()
    {
        ProjectSettings.SetSetting("display/screenshots/folder", Folder.PathJoin("auto"));
        ProjectSettings.SetSetting("display/screenshots/file_name", "{project} check");
        ProjectSettings.SetSetting("display/screenshots/format", "jpg");
        string path = ScreenshotServer.MakeScreenshotPath();
        var projectName = (string)ProjectSettings.GetSetting("application/config/name");
        ExpectEqual(path, Folder.PathJoin("auto").PathJoin(projectName + " check.jpg"), "MakeScreenshotPath uses folder, file name and format");
        Expect(!FileAccess.FileExists(path), "MakeScreenshotPath does not create the file");
    }

    private void TestFileNamePlaceholders()
    {
        long year = (long)Time.GetDateDictFromSystem()["year"];
        ProjectSettings.SetSetting("display/screenshots/file_name", "{year}_{unix}_{millisecond}");
        string[] parts = ScreenshotServer.MakeScreenshotPath().GetFile().GetBaseName().Split('_');
        ExpectEqual(parts[0], year.ToString("D4"), "{year} is the year with four digits");
        Expect(parts[1].IsValidInt(), "{unix} is a number");
        ExpectEqual(parts[2].Length, 3, "{millisecond} has three digits");
        ProjectSettings.SetSetting("display/screenshots/file_name", "a:b?c");
        string fileName = ScreenshotServer.MakeScreenshotPath().GetFile().GetBaseName();
        ExpectEqual(fileName, "a-b-c", "characters not allowed in file names become -");
        ProjectSettings.SetSetting("display/screenshots/file_name", "   ");
        fileName = ScreenshotServer.MakeScreenshotPath().GetFile().GetBaseName();
        ExpectEqual(fileName, "Screenshot", "an empty file name becomes Screenshot");
    }

    private void TestUniqueNames()
    {
        ProjectSettings.SetSetting("display/screenshots/file_name", "same");
        string first = ScreenshotServer.SaveScreenshot(_image);
        string second = ScreenshotServer.SaveScreenshot(_image);
        string third = ScreenshotServer.SaveScreenshot(_image);
        Expect(first.EndsWith("same.jpg"), "the first screenshot gets the plain name");
        Expect(second.EndsWith("same (2).jpg"), "the second one gets (2)");
        Expect(third.EndsWith("same (3).jpg"), "the third one gets (3)");
    }

    private void TestScreenshotSavedSignal()
    {
        int before = _saved.Count;
        string path = ScreenshotServer.SaveScreenshot(_image, Folder.PathJoin("signal.png"));
        ExpectEqual(_saved.Count, before + 1, "ScreenshotSaved is emitted once per saved file");
        ExpectEqual(_saved[before], path, "ScreenshotSaved carries the path");
    }

    private void TestCaptureInHeadless()
    {
        ExpectEqual(DisplayServer.GetName(), "headless", "the tests run headless");
        Expect(ScreenshotServer.CaptureViewport(GetViewport()) == null, "CaptureViewport gives null: nothing is rendered");
        Expect(ScreenshotServer.CaptureScreen() == null, "CaptureScreen gives null: no screen");
        ExpectEqual(ScreenshotServer.TakeScreenshot(), "", "TakeScreenshot gives an empty string");
        ExpectEqual(ScreenshotServer.TakeScreenScreenshot(0), "", "TakeScreenScreenshot gives an empty string");
    }

    private void RemoveSavedFiles()
    {
        foreach (string path in _saved)
        {
            DirAccess.RemoveAbsolute(path);
        }
    }

    private void OnScreenshotSaved(string path, Image image)
    {
        _saved.Add(path);
    }
}
