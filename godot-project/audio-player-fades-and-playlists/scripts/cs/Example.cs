using Godot;

namespace TruelyInteractive.AudioPlayerFadesAndPlaylists;

/// <summary>Minimal example of Audio Player Fades And Playlists: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Control
{
    private const string Res = "res://audio-player-fades-and-playlists/resources/";

    private readonly AudioStream MenuCalm = GD.Load<AudioStream>(Res + "title.tres");
    private readonly AudioStream MenuAction = GD.Load<AudioStream>(Res + "forest.tres");
    private bool _calm = true;
    private AudioStreamPlayer _music;
    private AudioStreamPlaylist _playlist;

    public override async void _Ready()
    {
        // Assign it to a player (music.tres is the playlist built in the Inspector).
        var music = GetNode<AudioStreamPlayer>("Music");
        music.Stream = GD.Load<AudioStream>(Res + "music.tres");
        music.FadeIn(2.0f);
        GD.Print("Playing: ", music.Playing);

        // Fades.
        var nextTrack = GD.Load<AudioStream>(Res + "river.tres");
        music.FadeIn(2.0f);
        music.FadeOut(1.0f, AudioMixerServer.FadeCurve.EaseOut);
        music.CrossfadeTo(nextTrack, 3.0f); // Equal power by default.
        GD.Print("Fading: ", music.IsFading(), ", fade gain: ", music.GetFadeGain());

        // Track info and signals.
        GD.Print("Track index: ", music.GetCurrentTrackIndex());

        // Playlist list and time helpers.
        var playlist = new AudioStreamPlaylist();
        playlist.AddStream(GD.Load<AudioStream>(Res + "forest.tres"));
        playlist.AddStream(GD.Load<AudioStream>(Res + "river.tres"));
        playlist.InsertStream(0, GD.Load<AudioStream>(Res + "title.tres"));
        playlist.MoveStream(2, 1);
        GD.Print(playlist.FindStream(GD.Load<AudioStream>(Res + "river.tres")));
        playlist.RemoveStream(0);

        playlist = GD.Load<AudioStreamPlaylist>(Res + "music.tres");
        for (int i = 0; i < playlist.StreamCount; i++)
        {
            GD.Print($"{i} {playlist.GetStreamName(i)}  length {playlist.GetStreamLength(i):0.0} s  starts at {playlist.GetStreamStartTime(i):0.0} s");
        }
        GD.Print("track at 3.5 s: ", playlist.GetStreamIndexAt(3.5));

        music.Stream = playlist;
        music.Play();
        GetNode<AudioStreamPlayer>("Music").Seek((float)playlist.GetStreamStartTime(2));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print("Track index after seek: ", music.GetCurrentTrackIndex());

        // Build a music menu with a crossfade (the button press is simulated).
        music.Stream = MenuCalm;
        music.FadeIn(1.5f);
        GetNode<Button>("SwitchButton").Pressed += OnSwitchPressed;
        GetNode<Button>("SwitchButton").EmitSignal(BaseButton.SignalName.Pressed);
        GD.Print("Menu music: ", music.Stream.ResourcePath.GetFile());

        // Show the playing track and a progress bar.
        _music = GetNode<AudioStreamPlayer>("Music");
        _music.Stream = playlist;
        _playlist = (AudioStreamPlaylist)_music.Stream;
        _music.PositionSignalInterval = 0.1f;
        _music.TrackChanged += OnTrackChanged;
        _music.PositionChanged += OnPositionChanged;
        _music.Play();
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        GD.Print("Now playing: ", GetNode<Label>("NowPlaying").Text, ", progress: ", GetNode<ProgressBar>("Progress").Value, " of ", GetNode<ProgressBar>("Progress").MaxValue);

        GetTree().Quit();
    }

    private void OnSwitchPressed()
    {
        _calm = !_calm;
        GetNode<AudioStreamPlayer>("Music").CrossfadeTo(_calm ? MenuCalm : MenuAction, 2.0f);
    }

    private void OnTrackChanged(long index, AudioStream stream)
    {
        GetNode<Label>("NowPlaying").Text = _playlist.GetStreamName((int)index);
        GetNode<ProgressBar>("Progress").MaxValue = _playlist.GetStreamLength((int)index);
    }

    private void OnPositionChanged(double position)
    {
        GetNode<ProgressBar>("Progress").Value = position;
    }
}
