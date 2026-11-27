using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TruelyInteractive.AudioTrackInformation;

/// <summary>Minimal example of Audio Track Information: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Control
{
    private const string Res = "res://audio-track-information/resources/";

    [Export] public Texture2D FallbackCover;
    private ItemList _list;
    private AudioStreamPlayer _player;
    private List<(string Path, AudioStream Info)> _tracks = [];

    public override void _Ready()
    {
        _list = GetNode<ItemList>("ItemList");
        _player = GetNode<AudioStreamPlayer>("AudioStreamPlayer");

        // Read the tags from code.
        AudioStream s = GD.Load<AudioStream>(Res + "night_drive.wav");
        GD.Print($"{s.Artist} - {s.Title} ({s.Album}, {s.Year})");
        GD.Print($"{s.GetCodec()}, {s.GetSampleRate()} Hz, {s.GetChannelCount()} ch, {s.GetBitrate() / 1000} kbit/s, {s.GetLength():0.0} s");

        // Music library list.
        Scan(Res);
        GD.Print("Library: ", _list.GetItemText(0));
        OnItemActivated(0);

        // Now playing with cover.
        ShowTrack(Res + "night_drive.wav");
        GD.Print("Now playing: ", GetNode<Label>("Label").Text.Replace("\n", " / "));

        // Format getters.
        AudioStream info = AudioStream.LoadInfo(Res + "night_drive.wav");
        GD.Print($"{info.GetCodec()}, {info.GetSampleRate()} Hz, {info.GetChannelCount()} ch, {info.GetBitrate() / 1000} kbit/s");

        // Runtime and import.
        GD.Print($"{info.Title} by {info.Artist}");

        // Playlists and wrapper streams.
        var album = new AudioStreamPlaylist
        {
            Title = "Night Drive",
            Artist = "Example Band",
            Year = 2026,
            Cover = GD.Load<Texture2D>(Res + "night_drive.png"),
        };
        GD.Print("Album: ", album.Title, ", cover ", album.Cover.GetSize());

        // Localized titles.
        var label = GetNode<Label>("Label");
        label.Text = TextLocalizationServer.Translate(s.Title);
        GD.Print("Label: ", label.Text);

        GetTree().Quit();
    }

    public void Scan(string dir)
    {
        foreach (string file in DirAccess.GetFilesAt(dir))
        {
            string ext = file.GetExtension().ToLower();
            if (ext != "mp3" && ext != "ogg" && ext != "opus" && ext != "wav")
            {
                continue;
            }
            string path = dir.PathJoin(file);
            AudioStream info = AudioStream.LoadInfo(path);
            if (info != null)
            {
                _tracks.Add((path, info));
            }
        }
        _tracks = _tracks
            .OrderBy(t => t.Info.Album)
            .ThenBy(t => t.Info.DiscNumber)
            .ThenBy(t => t.Info.TrackNumber)
            .ToList();
        _list.Clear();
        foreach (var (_, s) in _tracks)
        {
            int length = (int)s.GetLength();
            _list.AddItem($"{s.Artist} - {s.Title} ({length / 60}:{length % 60:00})");
        }
    }

    private void OnItemActivated(long index)
    {
        _player.Stream = GD.Load<AudioStream>(_tracks[(int)index].Path);
        _player.Play();
    }

    public void ShowTrack(string path)
    {
        var player = GetNode<AudioStreamPlayer>("AudioStreamPlayer");
        var coverRect = GetNode<TextureRect>("Cover");
        var label = GetNode<Label>("Label");
        AudioStream stream = GD.Load<AudioStream>(path);
        player.Stream = stream;
        player.Play();
        coverRect.Texture = stream.Cover ?? FallbackCover;
        label.Text = $"{stream.Title}\n{stream.Artist} - {stream.Album}";
    }
}
