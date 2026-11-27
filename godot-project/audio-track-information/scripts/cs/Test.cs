using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AudioTrackInformation;

/// <summary>
/// Full API test of Audio Track Information: the tag properties, format getters and LoadInfo() of AudioStream.
/// Same tests as scripts/gd/Test.gd.
/// resources/test/sample.wav has RIFF INFO tags and an id3 chunk, resources/test/sample.mp3 an ID3v2.3 tag (UTF-16, Latin-1,
/// an 8x8 PNG cover), 40 silent frames and an ID3v1 tag. Both are kept as files (not imported).
/// </summary>
public partial class Test : FeatureTest
{
    private const string WavPath = "res://audio-track-information/resources/test/sample.wav";
    private const string Mp3Path = "res://audio-track-information/resources/test/sample.mp3";
    private const string ImportedPath = "res://audio-track-information/resources/night_drive.wav";
    private const int WavSampleBytes = 1600;
    private const int WavBitrate = 128000; // 8000 Hz * 16 bit
    private const double Mp3Length = 40.0 * 1152.0 / 44100.0; // 40 frames of 1152 samples

    protected override Task RunTests()
    {
        TestWavTags();
        TestMp3Tags();
        TestLoadFromFile();
        TestImportedTags();
        TestDefaultsAndManualValues();
        TestWrapperStreams();
        TestReadFileTagsSetting();
        return Task.CompletedTask;
    }

    private void TestWavTags()
    {
        AudioStream stream = AudioStream.LoadInfo(WavPath);
        Expect(stream is AudioStreamWav, "LoadInfo of a WAV returns an AudioStreamWav");
        ExpectEqual(stream.Title, "Wave Title", "WAV: Title from INAM");
        ExpectEqual(stream.Artist, "Wave Artist", "WAV: Artist from IART");
        ExpectEqual(stream.Album, "Älbum ✓", "WAV: Album from the UTF-8 id3 chunk");
        ExpectEqual(stream.Composer, "Composer", "WAV: Composer from the id3 chunk");
        ExpectEqual(stream.TrackNumber, 7, "WAV: TrackNumber from ITRK");
        ExpectEqual(stream.Year, 1999, "WAV: Year from ICRD");
        ExpectEqual(stream.Genre, "Jazz", "WAV: Genre from IGNR");
        ExpectEqual(stream.GetSampleRate(), 8000, "WAV: GetSampleRate");
        ExpectEqual(stream.GetChannelCount(), 1, "WAV: GetChannelCount");
        ExpectEqual(stream.GetCodec(), "PCM", "WAV: GetCodec");
        ExpectEqual(stream.GetBitrate(), WavBitrate, "WAV: GetBitrate");
        Expect(Math.Abs(stream.GetLength() - 0.1) < 0.01, "WAV: GetLength is 0.1 s");
        Expect(stream.Cover == null, "WAV: no Cover");
        Expect(stream.CoverData.Length == 0, "WAV: no CoverData");
    }

    private void TestMp3Tags()
    {
        AudioStream stream = AudioStream.LoadInfo(Mp3Path);
        Expect(stream is AudioStreamMP3, "LoadInfo of an MP3 returns an AudioStreamMP3");
        ExpectEqual(stream.Title, "Nine Thou (Superstars Remix)", "MP3: Title from UTF-16 with BOM, ID3v2 wins over ID3v1");
        ExpectEqual(stream.Artist, "Café", "MP3: Artist from Latin-1");
        ExpectEqual(stream.TrackNumber, 3, "MP3: TrackNumber from TRCK n/total");
        ExpectEqual(stream.TrackCount, 12, "MP3: TrackCount from TRCK n/total");
        ExpectEqual(stream.DiscNumber, 1, "MP3: DiscNumber from TPOS n/total");
        ExpectEqual(stream.DiscCount, 2, "MP3: DiscCount from TPOS n/total");
        ExpectEqual(stream.Year, 2001, "MP3: Year from TYER");
        ExpectEqual(stream.Genre, "Rock", "MP3: Genre from the ID3 code (17)");
        ExpectEqual(stream.Comment, "Hello", "MP3: Comment from COMM");
        ExpectEqual(stream.GetSampleRate(), 44100, "MP3: GetSampleRate");
        ExpectEqual(stream.GetChannelCount(), 2, "MP3: GetChannelCount");
        ExpectEqual(stream.GetCodec(), "MP3", "MP3: GetCodec");
        Expect(Math.Abs(stream.GetBitrate() - 128000) < 12800, "MP3: GetBitrate is about 128 kbit/s");
        Expect(Math.Abs(stream.GetLength() - Mp3Length) < 0.1, "MP3: GetLength is about 1.04 s");
        Expect(stream.CoverData.Length > 0, "MP3: CoverData holds the APIC bytes");
        Texture2D cover = stream.Cover;
        ExpectEqual(cover.GetSize(), new Vector2(8, 8), "MP3: Cover is the decoded 8x8 picture");
        Expect(stream.GetCover() == cover, "MP3: the cover is decoded once and cached");
    }

    private void TestLoadFromFile()
    {
        AudioStreamWav wav = AudioStreamWav.LoadFromFile(WavPath);
        ExpectEqual(wav.Title, "Wave Title", "AudioStreamWav.LoadFromFile reads the tags");
        ExpectEqual(wav.Data.Length, WavSampleBytes, "AudioStreamWav.LoadFromFile keeps the audio data");
        AudioStreamMP3 mp3 = AudioStreamMP3.LoadFromFile(Mp3Path);
        ExpectEqual(mp3.Title, "Nine Thou (Superstars Remix)", "AudioStreamMP3.LoadFromFile reads the tags");
    }

    private void TestImportedTags()
    {
        AudioStream stream = GD.Load<AudioStream>(ImportedPath);
        ExpectEqual(stream.Title, "Night Drive", "the importer stores the tags in the imported resource");
        ExpectEqual(stream.GetCodec(), "QOA", "an imported WAV reports the codec of the imported data");
    }

    private void TestDefaultsAndManualValues()
    {
        var stream = new AudioStreamWav();
        foreach (string property in new[] { "title", "artist", "album", "album_artist", "genre", "composer", "copyright", "comment" })
        {
            ExpectEqual((string)stream.Get(property), "", $"{property} is empty by default");
        }
        foreach (string property in new[] { "track_number", "track_count", "disc_number", "disc_count", "year" })
        {
            ExpectEqual((int)stream.Get(property), 0, $"{property} is 0 by default");
        }
        Expect(stream.Cover == null, "Cover is null by default");
        Expect(stream.CoverData.Length == 0, "CoverData is empty by default");
        stream.Title = "By Hand";
        stream.TrackNumber = 2;
        ExpectEqual(stream.Title, "By Hand", "Title can be set by hand");
        ExpectEqual(stream.TrackNumber, 2, "TrackNumber can be set by hand");
        AudioStream mp3 = AudioStream.LoadInfo(Mp3Path);
        Image image = Image.CreateEmpty(4, 4, false, Image.Format.Rgb8);
        mp3.Cover = ImageTexture.CreateFromImage(image);
        ExpectEqual(mp3.Cover.GetSize(), new Vector2(4, 4), "Cover can be set by hand");
        Expect(mp3.CoverData.Length == 0, "setting a cover texture clears CoverData");
    }

    private void TestWrapperStreams()
    {
        var playlist = new AudioStreamPlaylist { Title = "Album", Artist = "Someone", Year = 2020 };
        ExpectEqual(playlist.Title, "Album", "a playlist has its own Title");
        ExpectEqual(playlist.Year, 2020, "a playlist has its own Year");
        ExpectEqual(playlist.GetCodec(), "", "a playlist has no codec");
        ExpectEqual(playlist.GetSampleRate(), 0, "a playlist has no sample rate");
    }

    private void TestReadFileTagsSetting()
    {
        ExpectEqual((bool)ProjectSettings.GetSetting("audio/info/read_file_tags"), true, "audio/info/read_file_tags defaults to true");
        ProjectSettings.SetSetting("audio/info/read_file_tags", false);
        AudioStreamWav off = AudioStreamWav.LoadFromFile(WavPath);
        ExpectEqual(off.Title, "", "read_file_tags off: no tags are read");
        ProjectSettings.SetSetting("audio/info/read_file_tags", true);
        AudioStreamWav on = AudioStreamWav.LoadFromFile(WavPath);
        ExpectEqual(on.Title, "Wave Title", "read_file_tags on: the tags are read again");
    }
}
