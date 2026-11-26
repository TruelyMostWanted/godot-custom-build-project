using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue265TaskTruelyVersion;

/// <summary>C# checks of 265-task-truely-version. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>
/// The Truely version comes from truely/version.txt of the engine (SemVer, first version 0.1.0).
/// The checks do not pin one number, so they stay green when the version is raised.
/// </remarks>
public partial class Issue265TaskTruelyVersionCheck : FeatureCheckCs
{
    protected override string CheckName => "265-task-truely-version";

    protected override Task RunChecks()
    {
        Godot.Collections.Dictionary info = Engine.GetVersionInfo();

        // The new keys exist and have the right types.
        foreach (string key in new[] { "truely_major", "truely_minor", "truely_patch", "truely_hex" })
        {
            Expect(info.ContainsKey(key), $"get_version_info has {key}");
            Expect(info.ContainsKey(key) && info[key].VariantType == Variant.Type.Int, $"{key} is an int");
        }
        Expect(info.ContainsKey("truely_version"), "get_version_info has truely_version");
        Expect(info.ContainsKey("truely_version") && info["truely_version"].VariantType == Variant.Type.String, "truely_version is a String");

        long major = info.ContainsKey("truely_major") ? (long)info["truely_major"] : -1;
        long minor = info.ContainsKey("truely_minor") ? (long)info["truely_minor"] : -1;
        long patch = info.ContainsKey("truely_patch") ? (long)info["truely_patch"] : -1;
        long hex = info.ContainsKey("truely_hex") ? (long)info["truely_hex"] : -1;
        string text = info.ContainsKey("truely_version") ? (string)info["truely_version"] : "";

        // The string always has three numbers and matches the ints.
        Expect(text == $"{major}.{minor}.{patch}", "truely_version is MAJOR.MINOR.PATCH of the ints");
        Expect(text.Split('.').Length == 3, "truely_version has three numbers");
        Expect(hex == ((major << 16) | (minor << 8) | patch), "truely_hex encodes the version like hex");

        // The first release is 0.1.0; the version never goes below it.
        Expect(major >= 0 && minor >= 0 && patch >= 0, "version numbers are not negative");
        Expect(hex >= 0x000100, "Truely version is 0.1.0 or newer");

        // The Godot base version keys are unchanged.
        Expect((int)info["major"] == 4, "Godot major version is still 4");
        foreach (string key in new[] { "minor", "patch", "hex", "status", "build", "hash", "timestamp", "string" })
        {
            Expect(info.ContainsKey(key), $"Godot key {key} is still there");
        }
        return Task.CompletedTask;
    }
}
