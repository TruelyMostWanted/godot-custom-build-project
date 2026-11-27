using System.Collections.Generic;
using Godot;

namespace TruelyInteractive.SubtitlePlayer;

using SubtitlePlayer = Godot.SubtitlePlayer;

/// <summary>
/// Minimal example of Subtitle Player: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.
/// The scene has Voice (an AudioStreamPlayer with voice.tres), SubtitlePlayer (night_drive.srt, own clock) and SubtitleLabel.
/// </summary>
public partial class Example : Node
{
    private const string Res = "res://subtitle-player/resources/";

    private readonly SubtitlePlayback _playback = new SubtitlePlayback();
    private double _time;

    public override async void _Ready()
    {
        SetProcess(false); // _Process belongs to "Using SubtitlePlayback without a node" below
        var label = GetNode<Label>("SubtitleLabel");

        // Give a stream a subtitle file.
        var voice = GetNode<AudioStreamPlayer>("Voice");
        voice.Stream.SubtitlePath = Res + "night_drive.srt";
        voice.SubtitlePath = Res + "night_drive_alt.srt";

        // Add a SubtitlePlayer.
        var player = new SubtitlePlayer();
        GetNode("Voice").AddChild(player);
        player.SyncNode = "..";

        // Show the text and play.
        player.SegmentChanged += segment =>
        {
            label.Text = segment?.Text ?? "";
            GD.Print(segment?.Text ?? "(none)");
        };
        voice.PlayingChanged += playing => GD.Print("playing_changed ", playing);
        voice.Play();
        await ToSignal(voice, AudioStreamPlayer.SignalName.Finished);

        // Play, pause, stop.
        GetNode<AudioStreamPlayer>("Voice").PlayingChanged += playing => GetNode<Label>("SubtitleLabel").Visible = playing;
        voice.Play();
        voice.Stop();
        GD.Print("Label visible after stop: ", label.Visible);

        // A playlist with a subtitle per track.
        var a = GD.Load<AudioStream>(Res + "line_a.tres");
        a.SubtitlePath = Res + "line_a.srt";
        var b = GD.Load<AudioStream>(Res + "line_b.tres");
        b.SubtitlePath = Res + "line_b.srt";
        voice.SubtitlePath = ""; // the override of the player would win over the streams
        voice.Stream = a;
        voice.Play();
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        GetNode<AudioStreamPlayer>("Voice").CrossfadeTo(b, 0.1f);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        GD.Print("After the crossfade: ", player.GetPlayback().GetSubtitle().ResourcePath);
        voice.Stop();

        // Explicit subtitle wins.
        var scenePlayer = GetNode<SubtitlePlayer>("SubtitlePlayer");
        GD.Print(scenePlayer.Subtitle.ResourcePath);
        GD.Print(scenePlayer.GetPlayback().GetSubtitle().ResourcePath);

        // Setting up by hand (the SubtitlePlayer of the scene, without a sync node).
        scenePlayer.SegmentChanged += segment =>
        {
            label.Text = segment != null ? SubtitleServer.GetDisplayText(scenePlayer.Subtitle, segment) : "";
            GD.Print("SubtitlePlayer: ", label.Text);
        };

        // Sync sources (own clock).
        scenePlayer.Play(0.5);
        scenePlayer.Seek(1.5);
        GD.Print(scenePlayer.GetPlaybackPosition(), " ", scenePlayer.IsPlaying());
        scenePlayer.Stop();

        // Using SubtitlePlayback without a node.
        _playback.Subtitle = GD.Load<Subtitle>(Res + "night_drive.srt");
        _playback.SegmentChanged += segment => GetNode<Label>("SubtitleLabel").Text = segment?.Text ?? "";
        SetProcess(true);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        GD.Print("Playback without a node: ", label.Text);
        SetProcess(false);

        // Overlapping segments.
        SubtitlePlayback playback = scenePlayer.GetPlayback();
        playback.SegmentStarted += _ => Show(scenePlayer);
        playback.SegmentEnded += _ => Show(scenePlayer);
        scenePlayer.Play(0.5);
        GD.Print("Overlapping: ", label.Text);
        scenePlayer.Stop();

        GetTree().Quit();
    }

    public override void _Process(double delta)
    {
        _time += delta;
        _playback.Update(_time);
    }

    private void Show(SubtitlePlayer player)
    {
        var lines = new List<string>();
        foreach (SubtitleSegment segment in player.GetPlayback().GetActiveSegments())
        {
            lines.Add(SubtitleServer.GetDisplayText(player.Subtitle, segment));
        }
        GetNode<Label>("SubtitleLabel").Text = string.Join("\n", lines);
    }
}
