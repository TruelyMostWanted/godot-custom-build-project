using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue102FeatureWavLoader;

/// <summary>C# checks of 102-feature-wav-loader. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue102FeatureWavLoaderCheck : FeatureCheckCs
{
    private const string Dir = "user://102-feature-wav-loader-cs";
    private const string Res = "res://102-feature-wav-loader/resources";

    protected override string CheckName => "102-feature-wav-loader";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        CheckLoadFromUser();
        CheckSaveAndReload();
        CheckCompressedCannotBeSaved();
        CheckImporterPrecedence();
        await CheckThreadedLoad();
    }

    /// <summary>A stereo test stream, 64 frames (8-bit or 16-bit PCM).</summary>
    private static AudioStreamWav MakeStream(AudioStreamWav.FormatEnum format)
    {
        byte[] data;
        if (format == AudioStreamWav.FormatEnum.Format8Bits)
        {
            data = new byte[128];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)((i * 2) % 256);
            }
        }
        else
        {
            data = new byte[256];
            for (int i = 0; i < 128; i++)
            {
                short v = (short)((i * 997) % 65536 - 32768);
                data[i * 2] = (byte)(v & 0xFF);
                data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
        }
        return new AudioStreamWav
        {
            Format = format,
            Stereo = true,
            MixRate = 22050,
            Data = data,
        };
    }

    private void CheckLoadFromUser()
    {
        string path = Dir.PathJoin("source.wav");
        AudioStreamWav original = MakeStream(AudioStreamWav.FormatEnum.Format16Bits);
        ExpectEqual(original.SaveToWav(path), Error.Ok, "SaveToWav writes a test file to user://");

        Expect(ResourceLoader.Exists(path), "ResourceLoader.Exists() finds a .wav in user://");
        Expect(ResourceLoader.Exists(path, "AudioStream"), "Exists() with type hint AudioStream");
        Expect(ResourceLoader.GetRecognizedExtensionsForType("AudioStreamWAV").Contains("wav"), "wav is a recognized extension");

        var loaded = ResourceLoader.Load<AudioStreamWav>(path, null, ResourceLoader.CacheMode.Ignore);
        if (!Expect(loaded != null, "ResourceLoader.Load() returns an AudioStreamWav from user://"))
        {
            return;
        }
        ExpectEqual(loaded.Format, AudioStreamWav.FormatEnum.Format16Bits, "format");
        ExpectEqual(loaded.Stereo, true, "stereo");
        ExpectEqual(loaded.MixRate, 22050, "mix rate");
        Expect(loaded.Data.SequenceEqual(original.Data), "PCM data is unchanged");
        Expect(Mathf.IsEqualApprox(loaded.GetLength(), 64.0 / 22050.0), "length");
        Expect(loaded.InstantiatePlayback() != null, "the stream can be played");

        Resource first = GD.Load(path);
        Resource second = GD.Load(path);
        Expect(first != null && first == second, "Load() caches the stream");
        ExpectEqual(first?.ResourcePath ?? "", path, "ResourcePath of the cached stream");
    }

    private void CheckSaveAndReload()
    {
        foreach (var format in new[] { AudioStreamWav.FormatEnum.Format8Bits, AudioStreamWav.FormatEnum.Format16Bits })
        {
            AudioStreamWav original = MakeStream(format);
            string path = Dir.PathJoin($"saved_{(int)format}.wav");
            Expect(ResourceSaver.GetRecognizedExtensions(original).Contains("wav"), $"ResourceSaver offers .wav for format {(int)format}");
            ExpectEqual(ResourceSaver.Save(original, path), Error.Ok, $"ResourceSaver.Save() writes format {(int)format}");
            var loaded = ResourceLoader.Load<AudioStreamWav>(path, "AudioStreamWAV", ResourceLoader.CacheMode.Ignore);
            if (Expect(loaded != null, $"saved file of format {(int)format} loads again"))
            {
                ExpectEqual(loaded.Format, format, "format after save and load");
                Expect(loaded.Data.SequenceEqual(original.Data), $"data after save and load (format {(int)format})");
            }
        }
    }

    private void CheckCompressedCannotBeSaved()
    {
        AudioStreamWav stream = MakeStream(AudioStreamWav.FormatEnum.Format16Bits);
        stream.Format = AudioStreamWav.FormatEnum.Qoa;
        string path = Dir.PathJoin("qoa.wav");
        if (FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(path);
        }
        Expect(!ResourceSaver.GetRecognizedExtensions(stream).Contains("wav"), "no .wav offered for QOA data");
        ExpectEqual(ResourceSaver.Save(stream, path), Error.Unavailable, "ResourceSaver.Save() refuses QOA data");
        Expect(!FileAccess.FileExists(path), "nothing is written for QOA data");
    }

    private void CheckImporterPrecedence()
    {
        // imported_tone.wav.import sets compress/mode=2 (QOA): only the importer produces QOA data.
        var imported = GD.Load<AudioStreamWav>(Res.PathJoin("imported_tone.wav"));
        if (Expect(imported != null, "imported res:// WAV loads"))
        {
            ExpectEqual(imported.Format, AudioStreamWav.FormatEnum.Qoa, "imported res:// WAV comes from the importer (QOA)");
        }
        // kept_tone.wav uses import mode "Keep File": the runtime loader reads the raw file.
        var kept = GD.Load<AudioStreamWav>(Res.PathJoin("kept_tone.wav"));
        if (Expect(kept != null, "WAV with import mode Keep File loads"))
        {
            ExpectEqual(kept.Format, AudioStreamWav.FormatEnum.Format16Bits, "Keep File WAV comes from the runtime loader (16-bit)");
            ExpectEqual(kept.MixRate, 8000, "Keep File WAV mix rate");
        }
    }

    private async Task CheckThreadedLoad()
    {
        string path = Dir.PathJoin("threaded.wav");
        ExpectEqual(ResourceSaver.Save(MakeStream(AudioStreamWav.FormatEnum.Format16Bits), path), Error.Ok, "write a file for threaded loading");
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path, "AudioStreamWAV"), Error.Ok, "LoadThreadedRequest() accepts a user:// WAV");
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path);
        int frames = 0;
        while (status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            frames++;
            status = ResourceLoader.LoadThreadedGetStatus(path);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Loaded, "threaded load finishes");
        var stream = ResourceLoader.LoadThreadedGet(path) as AudioStreamWav;
        Expect(stream != null && stream.Data.Length == 256, "LoadThreadedGet() returns the AudioStreamWav");
    }
}
