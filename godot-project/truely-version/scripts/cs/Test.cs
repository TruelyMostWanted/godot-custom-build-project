using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TruelyVersion;

/// <summary>Full API test of the Truely version: the extra keys of Engine.GetVersionInfo(). Same tests as scripts/gd/Test.gd.</summary>
/// <remarks>
/// The version comes from truely/version.txt of the engine (SemVer). The tests do not pin one number, so they stay
/// green when the version is raised.
/// </remarks>
public partial class Test : FeatureTest
{
    private const long FirstVersionHex = 0x000100;

    private static readonly string[] TruelyKeys = ["truely_major", "truely_minor", "truely_patch", "truely_hex"];
    private static readonly string[] GodotKeys = ["major", "minor", "patch", "hex", "status", "build", "hash", "timestamp", "string"];

    private Godot.Collections.Dictionary _info;
    private long _major;
    private long _minor;
    private long _patch;
    private long _hex;
    private string _text;

    protected override Task RunTests()
    {
        _info = Engine.GetVersionInfo();
        _major = (long)_info["truely_major"];
        _minor = (long)_info["truely_minor"];
        _patch = (long)_info["truely_patch"];
        _hex = (long)_info["truely_hex"];
        _text = (string)_info["truely_version"];
        TestKeysAndTypes();
        TestVersionString();
        TestVersionHex();
        TestFirstVersion();
        TestGodotKeys();
        return Task.CompletedTask;
    }

    private void TestKeysAndTypes()
    {
        foreach (string key in TruelyKeys)
        {
            Expect(_info.ContainsKey(key), $"get_version_info has {key}");
            ExpectEqual(_info[key].VariantType, Variant.Type.Int, $"{key} is an int");
        }
        Expect(_info.ContainsKey("truely_version"), "get_version_info has truely_version");
        ExpectEqual(_info["truely_version"].VariantType, Variant.Type.String, "truely_version is a String");
    }

    private void TestVersionString()
    {
        ExpectEqual(_text, $"{_major}.{_minor}.{_patch}", "truely_version is MAJOR.MINOR.PATCH of the ints");
        ExpectEqual(_text.Split('.').Length, 3, "truely_version has three numbers");
    }

    private void TestVersionHex()
    {
        long encoded = (_major << 16) | (_minor << 8) | _patch;
        ExpectEqual(_hex, encoded, "truely_hex has one byte per number");
    }

    private void TestFirstVersion()
    {
        Expect(_major >= 0, "the major number is not negative");
        Expect(_minor >= 0, "the minor number is not negative");
        Expect(_patch >= 0, "the patch number is not negative");
        Expect(_hex >= FirstVersionHex, "the Truely version is 0.1.0 or newer");
    }

    private void TestGodotKeys()
    {
        ExpectEqual((long)_info["major"], 4L, "the Godot major version is still 4");
        foreach (string key in GodotKeys)
        {
            Expect(_info.ContainsKey(key), $"the Godot key {key} is still there");
        }
    }
}
