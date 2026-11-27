using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AudioFiles;

/// <summary>Full API test of Audio Files (runtime loaders and the WAV saver). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://audio-files-test";
    private const string Res = "res://audio-files/resources";
    private const int MixRate = 22050;
    private const int Frames = 64;
    private const double LengthTolerance = 0.1;

    // Compressed formats: extension, class, length in seconds. kept_tone.<extension> is a raw file (import mode Keep File).
    private static readonly (string Extension, string ClassName, double Length)[] Formats =
    [
        ("ogg", "AudioStreamOggVorbis", 0.5),
        ("mp3", "AudioStreamMP3", 0.5),
        ("opus", "AudioStreamOpus", 1.0),
    ];

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        TestWavLoad();
        TestWavSave();
        TestWavQoaRefused();
        TestCompressedFormats();
        TestInvalidFile();
        TestImporterPrecedence();
        await TestThreaded();
    }

    // A stereo stream of Frames frames: 2 bytes per frame and channel for 16 bits, 1 for 8 bits.
    private static AudioStreamWav MakeWav(AudioStreamWav.FormatEnum format)
    {
        int bytesPerSample = format == AudioStreamWav.FormatEnum.Format16Bits ? 2 : 1;
        byte[] data = new byte[Frames * 2 * bytesPerSample];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 7 % 256);
        }
        return new AudioStreamWav { Format = format, Stereo = true, MixRate = MixRate, Data = data };
    }

    private static void CopyKept(string extension, string target)
    {
        byte[] bytes = FileAccess.GetFileAsBytes($"{Res}/kept_tone.{extension}");
        using FileAccess file = FileAccess.Open(target, FileAccess.ModeFlags.Write);
        file.StoreBuffer(bytes);
    }

    private void TestWavLoad()
    {
        string path = Dir + "/source.wav";
        AudioStreamWav original = MakeWav(AudioStreamWav.FormatEnum.Format16Bits);
        ExpectEqual(original.SaveToWav(path), Error.Ok, "SaveToWav writes a .wav to user://");
        Expect(ResourceLoader.Exists(path), "ResourceLoader.Exists finds a .wav in user://");
        Expect(ResourceLoader.Exists(path, "AudioStream"), "ResourceLoader.Exists finds it with the hint AudioStream");
        Expect(Array.IndexOf(ResourceLoader.GetRecognizedExtensionsForType("AudioStreamWAV"), "wav") >= 0, "wav is a recognized extension");
        var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as AudioStreamWav;
        Expect(loaded != null, "ResourceLoader.Load returns an AudioStreamWav from user://");
        ExpectEqual(loaded.Format, AudioStreamWav.FormatEnum.Format16Bits, "format is 16 bits");
        Expect(loaded.Stereo, "stream is stereo");
        ExpectEqual(loaded.MixRate, MixRate, "MixRate is kept");
        Expect(loaded.Data.AsSpan().SequenceEqual(original.Data), "PCM data is unchanged");
        ExpectEqual((float)loaded.GetLength(), (float)Frames / MixRate, "length is frames / mix rate");
        Expect(loaded.InstantiatePlayback() != null, "the stream can be played");
        Resource first = GD.Load(path);
        Resource second = GD.Load(path);
        Expect(first == second, "Load caches the stream");
        ExpectEqual(first.ResourcePath, path, "ResourcePath is the loaded path");
    }

    private void TestWavSave()
    {
        foreach (AudioStreamWav.FormatEnum format in new[] { AudioStreamWav.FormatEnum.Format8Bits, AudioStreamWav.FormatEnum.Format16Bits })
        {
            AudioStreamWav original = MakeWav(format);
            string path = $"{Dir}/saved_{(int)format}.wav";
            Expect(Array.IndexOf(ResourceSaver.GetRecognizedExtensions(original), "wav") >= 0, $"ResourceSaver offers .wav for format {(int)format}");
            ExpectEqual(ResourceSaver.Save(original, path), Error.Ok, $"ResourceSaver.Save writes format {(int)format}");
            var loaded = ResourceLoader.Load(path, "AudioStreamWAV", ResourceLoader.CacheMode.Ignore) as AudioStreamWav;
            ExpectEqual(loaded.Format, format, $"format {(int)format} survives save and load");
            Expect(loaded.Data.AsSpan().SequenceEqual(original.Data), $"data of format {(int)format} survives save and load");
        }
    }

    private void TestWavQoaRefused()
    {
        AudioStreamWav stream = MakeWav(AudioStreamWav.FormatEnum.Format16Bits);
        stream.Format = AudioStreamWav.FormatEnum.Qoa;
        string path = Dir + "/qoa.wav";
        Expect(Array.IndexOf(ResourceSaver.GetRecognizedExtensions(stream), "wav") < 0, "no .wav is offered for QOA data");
        ExpectEqual(ResourceSaver.Save(stream, path), Error.Unavailable, "ResourceSaver.Save refuses QOA data");
        Expect(!FileAccess.FileExists(path), "nothing is written for QOA data");
    }

    private void TestCompressedFormats()
    {
        foreach ((string extension, string className, double length) in Formats)
        {
            string path = $"{Dir}/tone.{extension}";
            CopyKept(extension, path);
            Expect(ResourceLoader.Exists(path), $"ResourceLoader.Exists finds the .{extension}");
            Expect(ResourceLoader.Exists(path, "AudioStream"), $"ResourceLoader.Exists finds the .{extension} with the hint AudioStream");
            Expect(Array.IndexOf(ResourceLoader.GetRecognizedExtensionsForType(className), extension) >= 0, $"{extension} is a recognized extension");
            var stream = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as AudioStream;
            ExpectEqual(stream.GetClass(), className, $".{extension} loads as {className}");
            Expect(Math.Abs(stream.GetLength() - length) < LengthTolerance, $".{extension} length is about {length} s");
            Expect(!(bool)stream.Get("loop"), $".{extension} uses the importer default: no loop");
            Expect(stream.InstantiatePlayback() != null, $".{extension} can be played");
            Resource first = GD.Load(path);
            Resource second = GD.Load(path);
            Expect(first == second, $".{extension} is cached");
            Expect(Array.IndexOf(ResourceSaver.GetRecognizedExtensions(stream), extension) < 0, $"ResourceSaver offers no .{extension}");
            Expect(ResourceSaver.Save(stream, $"{Dir}/copy.{extension}") != Error.Ok, $"ResourceSaver.Save cannot write .{extension}");
        }
    }

    private void TestInvalidFile()
    {
        string path = Dir + "/broken.opus";
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("This is not an Ogg Opus file.");
        }
        Resource broken = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(broken == null, "an invalid .opus file does not load");
    }

    // The .import files of imported_tone.* set loop=true (WAV: compress/mode=2, QOA); Keep File files go to the runtime loader.
    private void TestImporterPrecedence()
    {
        var wav = GD.Load<AudioStreamWav>(Res + "/imported_tone.wav");
        ExpectEqual(wav.Format, AudioStreamWav.FormatEnum.Qoa, "imported WAV comes from the importer (QOA)");
        var keptWav = GD.Load<AudioStreamWav>(Res + "/kept_tone.wav");
        ExpectEqual(keptWav.Format, AudioStreamWav.FormatEnum.Format16Bits, "Keep File WAV comes from the runtime loader (16 bits)");
        ExpectEqual(keptWav.MixRate, 8000, "Keep File WAV keeps its mix rate");
        foreach (string extension in new[] { "ogg", "mp3" })
        {
            var imported = GD.Load<AudioStream>($"{Res}/imported_tone.{extension}");
            Expect((bool)imported.Get("loop"), $"imported .{extension} comes from the importer (loop is on)");
            var kept = GD.Load<AudioStream>($"{Res}/kept_tone.{extension}");
            Expect(!(bool)kept.Get("loop"), $"Keep File .{extension} comes from the runtime loader (loop is off)");
        }
    }

    private async Task TestThreaded()
    {
        string wavPath = Dir + "/threaded.wav";
        ResourceSaver.Save(MakeWav(AudioStreamWav.FormatEnum.Format16Bits), wavPath);
        await LoadThreaded(wavPath, "AudioStreamWAV");
        foreach ((string extension, string className, _) in Formats)
        {
            string path = $"{Dir}/threaded.{extension}";
            CopyKept(extension, path);
            await LoadThreaded(path, className);
        }
    }

    private async Task LoadThreaded(string path, string expectedClass)
    {
        string file = path.GetFile();
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path, expectedClass), Error.Ok, $"LoadThreadedRequest accepts {file}");
        while (ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        ExpectEqual(ResourceLoader.LoadThreadedGetStatus(path), ResourceLoader.ThreadLoadStatus.Loaded, $"the threaded load of {file} finishes");
        Resource stream = ResourceLoader.LoadThreadedGet(path);
        ExpectEqual(stream.GetClass(), expectedClass, $"LoadThreadedGet returns {expectedClass}");
    }
}
