using Godot;

namespace TruelyInteractive.AudioFiles;

/// <summary>Minimal example of Audio Files: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Setup: an Ogg Vorbis file in user://music and an AudioStreamPlayer named Music (the guide assumes them).
        DirAccess.MakeDirRecursiveAbsolute("user://music");
        DirAccess.MakeDirRecursiveAbsolute("user://recordings");
        DirAccess.CopyAbsolute("res://audio-files/resources/kept_tone.ogg", "user://music/theme.ogg");
        var musicPlayer = new AudioStreamPlayer { Name = "Music" };
        AddChild(musicPlayer);
        byte[] recordedPcm = new byte[8820];

        // Play music from the player's folder
        var music = GD.Load<AudioStreamOggVorbis>("user://music/theme.ogg");
        music.Loop = true;
        var player = GetNode<AudioStreamPlayer>("Music");
        player.Stream = music;
        player.Play();
        GD.Print("Music length: ", music.GetLength());

        // Save a recording as WAV
        var stream = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44100,
            Data = recordedPcm,
        };
        Error error = ResourceSaver.Save(stream, "user://recordings/take1.wav");
        GD.Print("Saved WAV: ", error == Error.Ok);
        player.Stop();
        GetTree().Quit();
    }
}
