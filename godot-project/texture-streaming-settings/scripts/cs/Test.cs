using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TextureStreamingSettings;

using TextureStreamingSettings = Godot.TextureStreamingSettings;

/// <summary>
/// Full API test of Texture Streaming Settings (TextureStreamingSettingsServer, TextureStreamingSettings). Same tests as scripts/gd/Test.gd.
/// The server only exists with the texture_streaming module, so it is reached through Engine.GetSingleton().
/// The renderer applies the values only on Forward+ and Mobile.
/// </summary>
public partial class Test : FeatureTest
{
    private const string StreamingFile = "user://settings/texture_streaming.ini";

    private GodotObject _streaming;
    private string[] _restartNames = [];

    protected override async Task RunTests()
    {
        if (!Engine.HasSingleton("TextureStreamingSettingsServer"))
        {
            GD.Print("  SKIP  this build has no texture streaming module");
            return;
        }
        _streaming = Engine.GetSingleton("TextureStreamingSettingsServer");
        _streaming.Call("reset_to_defaults");
        TestDefaults();
        TestValues();
        TestSupport();
        TestEngine();
        TestRestartRequired();
        TestResource();
        _streaming.Call("reset_to_defaults");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(StreamingFile);
    }

    private void TestDefaults()
    {
        int projectBudget = (int)ProjectSettings.GetSetting("rendering/textures/streaming/memory_budget_mb");
        ExpectEqual((int)_streaming.Call("get_memory_budget_mb"), projectBudget, "the default budget is the project setting");
        Expect(!(bool)_streaming.Call("is_memory_budget_enabled"), "the memory budget is off by default");
        ExpectEqual((int)_streaming.Call("get_min_lod"), 0, "the default min_lod is 0");
    }

    private void TestValues()
    {
        _streaming.Call("set_enabled", true);
        Expect((bool)_streaming.Call("is_enabled"), "enabled round trip");
        _streaming.Call("set_memory_budget_enabled", true);
        Expect((bool)_streaming.Call("is_memory_budget_enabled"), "memory_budget_enabled round trip");
        _streaming.Call("set_memory_budget_mb", 1024);
        ExpectEqual((int)_streaming.Call("get_memory_budget_mb"), 1024, "memory_budget_mb round trip");
        _streaming.Call("set_min_lod", 1);
        ExpectEqual((int)_streaming.Call("get_min_lod"), 1, "min_lod round trip");
        _streaming.Call("set_max_lod", 2);
        ExpectEqual((int)_streaming.Call("get_max_lod"), 2, "max_lod round trip");
        _streaming.Call("reset_to_defaults");
    }

    private void TestSupport()
    {
        bool supported = (bool)_streaming.Call("is_renderer_supported");
        bool compatibility = RenderingServer.GetCurrentRenderingMethod() == "gl_compatibility";
        ExpectEqual(supported, !compatibility, "streaming works on Forward+ and Mobile only");
        string[] unsupported = (string[])_streaming.Call("get_unsupported_setting_names");
        ExpectEqual(unsupported.Length == 0, supported, "get_unsupported_setting_names is empty when the renderer supports streaming");
    }

    private void TestEngine()
    {
        if (!(bool)_streaming.Call("is_renderer_supported"))
        {
            GD.Print("  SKIP  engine checks (the renderer has no texture streaming)");
            return;
        }
        GodotObject textureStreaming = Engine.GetSingleton("TextureStreaming");
        _streaming.Call("set_memory_budget_enabled", true);
        _streaming.Call("set_memory_budget_mb", 1);
        _streaming.Call("set_min_lod", 2);
        ExpectEqual((int)textureStreaming.Get("memory_budget_mb_override"), 1, "memory_budget_mb reaches TextureStreaming at once");
        ExpectEqual((int)textureStreaming.Get("min_lod_override"), 2, "min_lod reaches TextureStreaming at once");
        bool memoryEnabled = (bool)ProjectSettings.GetSetting("rendering/textures/streaming/memory_budget_enabled");
        Expect(memoryEnabled, "memory_budget_enabled reaches the project setting");
        long used = (long)_streaming.Call("get_memory_budget_bytes_used");
        Expect(used >= 0, "get_memory_budget_bytes_used is not negative");
        _streaming.Call("reset_to_defaults");
    }

    private void TestRestartRequired()
    {
        _streaming.Connect("restart_required", Callable.From((string[] names) => _restartNames = names));
        _streaming.Call("set_enabled", true);
        Expect(System.Array.IndexOf(_restartNames, "enabled") >= 0, "restart_required is emitted with \"enabled\"");
        string[] pending = (string[])_streaming.Call("get_restart_required_names");
        Expect(System.Array.IndexOf(pending, "enabled") >= 0, "get_restart_required_names lists \"enabled\"");
        _streaming.Call("reset_to_defaults");
    }

    private void TestResource()
    {
        var settings = new TextureStreamingSettings();
        Expect(!settings.Enabled, "TextureStreamingSettings: not enabled by default");
        Expect(!settings.MemoryBudgetEnabled, "TextureStreamingSettings: memory budget off by default");
        ExpectEqual(settings.MemoryBudgetMb, 512, "TextureStreamingSettings: default MemoryBudgetMb");
        ExpectEqual(settings.MinLod, 0, "TextureStreamingSettings: default MinLod");
        ExpectEqual(settings.MaxLod, 3, "TextureStreamingSettings: default MaxLod");
        settings.MemoryBudgetMb = 256;
        ExpectEqual(settings.MemoryBudgetMb, 256, "TextureStreamingSettings: MemoryBudgetMb is a plain property");
    }
}
