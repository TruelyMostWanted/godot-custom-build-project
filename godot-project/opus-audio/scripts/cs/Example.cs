using Godot;

namespace TruelyInteractive.OpusAudio;

/// <summary>Minimal example of Opus Audio: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Setup: a copy of the sample in user://downloads, its bytes, and AudioStreamPlayers Music and Player (the guide assumes them).
        DirAccess.MakeDirRecursiveAbsolute("user://downloads");
        DirAccess.CopyAbsolute("res://opus-audio/resources/sine_440.opus", "user://downloads/voice.opus");
        byte[] downloadedBytes = FileAccess.GetFileAsBytes("user://downloads/voice.opus");
        foreach (string playerName in new[] { "Music", "Player" })
        {
            AddChild(new AudioStreamPlayer { Name = playerName });
        }

        // Import an Opus file (the sample is imported as res://opus-audio/resources/sine_440.opus)
        var music = GD.Load<AudioStreamOpus>("res://opus-audio/resources/sine_440.opus");
        music.Loop = true;
        var player = GetNode<AudioStreamPlayer>("Music");
        player.Stream = music;
        player.Play();
        GD.Print("Music length: ", music.GetLength());

        // Load at runtime
        var voice = GD.Load<AudioStreamOpus>("user://downloads/voice.opus");
        voice.Loop = true;
        var voicePlayer = GetNode<AudioStreamPlayer>("Player");
        voicePlayer.Stream = voice;
        voicePlayer.Play();
        AudioStreamOpus fromBuffer = AudioStreamOpus.LoadFromBuffer(downloadedBytes);
        GD.Print("From buffer: ", fromBuffer.GetLength());

        // Play a stream
        AudioStream stream = fromBuffer;
        AudioStreamPlayback playback = stream.InstantiatePlayback();
        playback.Start(0.0);
        GD.Print("Playback: ", playback.GetClass());
        player.Stop();
        voicePlayer.Stop();
        GetTree().Quit();
    }
}
