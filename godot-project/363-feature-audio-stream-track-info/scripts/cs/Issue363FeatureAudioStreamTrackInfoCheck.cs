using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue363FeatureAudioStreamTrackInfo;

/// <summary>C# checks of 363-feature-audio-stream-track-info. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>The sample files are generated into user:// by the check itself.</remarks>
public partial class Issue363FeatureAudioStreamTrackInfoCheck : FeatureCheckCs
{
    private const string Dir = "user://363-feature-audio-stream-track-info-cs";
    private const string WavPath = Dir + "/sample.wav";
    private const string Mp3Path = Dir + "/sample.mp3";

    protected override string CheckName => "363-feature-audio-stream-track-info";

    // --- byte helpers ---------------------------------------------------------

    private static byte[] Bytes(params byte[] values) => values;

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Le16(int v) => Bytes((byte)(v & 0xFF), (byte)((v >> 8) & 0xFF));

    private static byte[] Le32(int v) => Bytes((byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF));

    private static byte[] Be32(int v) => Bytes((byte)((v >> 24) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 8) & 0xFF), (byte)(v & 0xFF));

    private static byte[] Syncsafe(int v) => Bytes((byte)((v >> 21) & 0x7F), (byte)((v >> 14) & 0x7F), (byte)((v >> 7) & 0x7F), (byte)(v & 0x7F));

    private static byte[] Concat(params byte[][] parts)
    {
        var list = new List<byte>();
        foreach (var part in parts)
        {
            list.AddRange(part);
        }
        return list.ToArray();
    }

    /// <summary>A RIFF chunk: id, little endian size, body, padding to an even size.</summary>
    private static byte[] RiffChunk(string id, byte[] body)
        => Concat(Ascii(id), Le32(body.Length), body, body.Length % 2 == 1 ? Bytes(0) : Array.Empty<byte>());

    /// <summary>An INFO sub-chunk with a zero terminated string.</summary>
    private static byte[] Info(string id, string text) => RiffChunk(id, Concat(Ascii(text), Bytes(0)));

    /// <summary>An ID3v2 text frame body: encoding byte + text bytes.</summary>
    private static byte[] TextPayload(byte encoding, byte[] text) => Concat(Bytes(encoding), text);

    /// <summary>An ID3v2 frame. version 4: syncsafe size, version 3: big endian size.</summary>
    private static byte[] Frame(int version, string id, byte[] payload)
        => Concat(Ascii(id), version == 4 ? Syncsafe(payload.Length) : Be32(payload.Length), Bytes(0, 0), payload);

    private static byte[] Id3Tag(byte version, byte[] frames)
        => Concat(Ascii("ID3"), Bytes(version, 0, 0), Syncsafe(frames.Length), frames);

    /// <summary>A zero padded fixed size field of an ID3v1 tag.</summary>
    private static byte[] Field(string text, int size)
    {
        var bytes = new byte[size];
        Ascii(text).CopyTo(bytes, 0);
        return bytes;
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreBuffer(bytes);
    }

    // --- sample files ---------------------------------------------------------

    private static byte[] MakeWavBytes()
    {
        byte[] fmt = Concat(Le16(1), Le16(1), Le32(8000), Le32(16000), Le16(2), Le16(16));
        byte[] info = Concat(
            Ascii("INFO"),
            Info("INAM", "Wave Title"),
            Info("IART", "Wave Artist"),
            Info("ITRK", "7"),
            Info("ICRD", "1999"),
            Info("IGNR", "Jazz"));
        byte[] frames = Concat(
            Frame(4, "TALB", TextPayload(3, Encoding.UTF8.GetBytes("Älbum ✓"))),
            Frame(4, "TCOM", TextPayload(3, Encoding.UTF8.GetBytes("Composer"))));
        byte[] body = Concat(
            Ascii("WAVE"),
            RiffChunk("fmt ", fmt),
            RiffChunk("data", new byte[1600]),
            RiffChunk("LIST", info),
            RiffChunk("id3 ", Id3Tag(4, frames)));
        return Concat(Ascii("RIFF"), Le32(body.Length), body);
    }

    private static byte[] MakeMp3Bytes()
    {
        var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgb8);
        image.Fill(Colors.Red);
        byte[] png = image.SavePngToBuffer();
        byte[] title = Concat(Bytes(0xFF, 0xFE), Encoding.Unicode.GetBytes("Nine Thou (Superstars Remix)"));
        byte[] comm = Concat(TextPayload(0, Ascii("eng")), Bytes(0), Ascii("Hello"));
        byte[] apic = Concat(TextPayload(0, Ascii("image/png")), Bytes(0, 3, 0), png);
        byte[] frames = Concat(
            Frame(3, "TIT2", TextPayload(1, title)),
            Frame(3, "TPE1", TextPayload(0, Bytes(0x43, 0x61, 0x66, 0xE9))),
            Frame(3, "TRCK", TextPayload(0, Ascii("3/12"))),
            Frame(3, "TPOS", TextPayload(0, Ascii("1/2"))),
            Frame(3, "TYER", TextPayload(0, Ascii("2001"))),
            Frame(3, "TCON", TextPayload(0, Ascii("(17)"))),
            Frame(3, "COMM", comm),
            Frame(3, "APIC", apic));
        var parts = new List<byte[]> { Id3Tag(3, frames) };
        for (int i = 0; i < 40; i++)
        {
            parts.Add(Bytes(0xFF, 0xFB, 0x90, 0x64));
            parts.Add(new byte[413]);
        }
        parts.Add(Concat(Ascii("TAG"), Field("Old", 30), Field("", 30), Field("", 30), Field("", 4), Field("", 30), Bytes(17)));
        return Concat(parts.ToArray());
    }

    // --- checks ---------------------------------------------------------------

    private static bool Near(double actual, double expected, double toleranceFraction)
        => Math.Abs(actual - expected) <= expected * toleranceFraction;

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        WriteFile(WavPath, MakeWavBytes());
        WriteFile(Mp3Path, MakeMp3Bytes());
        CheckWav(AudioStream.LoadInfo(WavPath), "WAV load_info");
        CheckMp3(AudioStream.LoadInfo(Mp3Path), "MP3 load_info");
        var wav = AudioStreamWav.LoadFromFile(WavPath);
        if (Expect(wav != null, "AudioStreamWAV.load_from_file returns a stream"))
        {
            CheckWav(wav, "WAV load_from_file");
            Expect(wav.Data.Length == 1600, "WAV load_from_file has the 1600 sample bytes");
        }
        var mp3 = AudioStreamMP3.LoadFromFile(Mp3Path);
        if (Expect(mp3 != null, "AudioStreamMP3.load_from_file returns a stream (decoder accepts the zero frames)"))
        {
            CheckMp3(mp3, "MP3 load_from_file");
        }
        CheckFreshAndManual();
        CheckSetting();
        return Task.CompletedTask;
    }

    private void CheckWav(AudioStream stream, string label)
    {
        if (!Expect(stream != null, $"{label}: returns a stream"))
        {
            return;
        }
        Expect(stream is AudioStreamWav, $"{label}: class is AudioStreamWAV");
        ExpectEqual(stream.Title, "Wave Title", $"{label}: title");
        ExpectEqual(stream.Artist, "Wave Artist", $"{label}: artist");
        ExpectEqual(stream.Album, "Älbum ✓", $"{label}: album (UTF-8 from id3 chunk)");
        ExpectEqual(stream.Composer, "Composer", $"{label}: composer");
        ExpectEqual(stream.TrackNumber, 7L, $"{label}: track_number");
        ExpectEqual(stream.Year, 1999L, $"{label}: year");
        ExpectEqual(stream.Genre, "Jazz", $"{label}: genre");
        ExpectEqual(stream.GetSampleRate(), 8000L, $"{label}: sample rate");
        ExpectEqual(stream.GetChannelCount(), 1L, $"{label}: channel count");
        ExpectEqual(stream.GetCodec(), "PCM", $"{label}: codec");
        ExpectEqual(stream.GetBitrate(), 128000L, $"{label}: bitrate (8000 * 16)");
        Expect(Near(stream.GetLength(), 0.1, 0.1), $"{label}: length about 0.1 s (got {stream.GetLength()})");
        Expect(stream.Cover == null, $"{label}: no cover");
        Expect(stream.CoverData.Length == 0, $"{label}: no cover data");
    }

    private void CheckMp3(AudioStream stream, string label)
    {
        if (!Expect(stream != null, $"{label}: returns a stream"))
        {
            return;
        }
        Expect(stream is AudioStreamMP3, $"{label}: class is AudioStreamMP3");
        ExpectEqual(stream.Title, "Nine Thou (Superstars Remix)", $"{label}: title (UTF-16 with BOM, ID3v2 wins over ID3v1)");
        ExpectEqual(stream.Artist, "Café", $"{label}: artist (Latin-1)");
        ExpectEqual(stream.TrackNumber, 3L, $"{label}: track_number");
        ExpectEqual(stream.TrackCount, 12L, $"{label}: track_count");
        ExpectEqual(stream.DiscNumber, 1L, $"{label}: disc_number");
        ExpectEqual(stream.DiscCount, 2L, $"{label}: disc_count");
        ExpectEqual(stream.Year, 2001L, $"{label}: year");
        ExpectEqual(stream.Genre, "Rock", $"{label}: genre from (17)");
        ExpectEqual(stream.Comment, "Hello", $"{label}: comment");
        ExpectEqual(stream.GetSampleRate(), 44100L, $"{label}: sample rate");
        ExpectEqual(stream.GetChannelCount(), 2L, $"{label}: channel count");
        ExpectEqual(stream.GetCodec(), "MP3", $"{label}: codec");
        Expect(Near(stream.GetBitrate(), 128000.0, 0.1), $"{label}: bitrate about 128000 (got {stream.GetBitrate()})");
        Expect(Near(stream.GetLength(), 40.0 * 1152.0 / 44100.0, 0.1), $"{label}: length about 1.04 s (got {stream.GetLength()})");
        Expect(stream.CoverData.Length > 0, $"{label}: cover_data not empty");
        Texture2D cover = stream.Cover;
        if (Expect(cover != null, $"{label}: cover is a Texture2D"))
        {
            Expect(cover.GetWidth() == 8 && cover.GetHeight() == 8, $"{label}: cover is 8x8");
            Expect(stream.Cover == cover, $"{label}: cover getter returns the cached cover");
        }
    }

    private void CheckFreshAndManual()
    {
        var fresh = new AudioStreamWav();
        Expect(fresh.Title == "" && fresh.Artist == "" && fresh.Album == "" && fresh.AlbumArtist == "", "fresh stream: text fields empty");
        Expect(fresh.Composer == "" && fresh.Copyright == "" && fresh.Genre == "" && fresh.Comment == "", "fresh stream: other text fields empty");
        Expect(fresh.TrackNumber == 0 && fresh.TrackCount == 0 && fresh.DiscNumber == 0 && fresh.DiscCount == 0 && fresh.Year == 0, "fresh stream: numbers 0");
        Expect(fresh.Cover == null, "fresh stream: cover null");
        Expect(fresh.CoverData.Length == 0, "fresh stream: cover_data empty");
        fresh.Title = "By Hand";
        fresh.TrackNumber = 2;
        var image = Image.CreateEmpty(4, 4, false, Image.Format.Rgb8);
        fresh.Cover = ImageTexture.CreateFromImage(image);
        ExpectEqual(fresh.Title, "By Hand", "title set by hand");
        ExpectEqual(fresh.TrackNumber, 2L, "track_number set by hand");
        Expect(fresh.Cover != null && fresh.Cover.GetWidth() == 4, "cover set by hand");
        Expect(fresh.CoverData.Length == 0, "setting a cover texture clears cover_data");
        var playlist = new AudioStreamPlaylist();
        Expect(playlist.Title == "" && playlist.Cover == null && playlist.TrackNumber == 0, "playlist has the fields, empty by default");
        playlist.Title = "Album";
        playlist.Artist = "Someone";
        playlist.Year = 2020;
        Expect(playlist.Title == "Album" && playlist.Artist == "Someone" && playlist.Year == 2020, "playlist fields set by hand");
        ExpectEqual(playlist.GetCodec(), "", "playlist codec is empty");
        ExpectEqual(playlist.GetSampleRate(), 0L, "playlist sample rate is 0");
    }

    private void CheckSetting()
    {
        Expect(ProjectSettings.GetSetting("audio/info/read_file_tags", true).AsBool(), "audio/info/read_file_tags defaults to true");
        ProjectSettings.SetSetting("audio/info/read_file_tags", false);
        var off = AudioStreamWav.LoadFromFile(WavPath);
        if (Expect(off != null, "load_from_file works with read_file_tags off"))
        {
            ExpectEqual(off.Title, "", "title is empty with read_file_tags off");
        }
        ProjectSettings.SetSetting("audio/info/read_file_tags", true);
        var on = AudioStreamWav.LoadFromFile(WavPath);
        if (Expect(on != null, "load_from_file works with read_file_tags on again"))
        {
            ExpectEqual(on.Title, "Wave Title", "title is read again with read_file_tags on");
        }
    }
}
