using System.Threading.Tasks;
using Godot;

namespace TruelyInteractive.AudioMixer;

/// <summary>Minimal example of Audio Mixer: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Tone = "res://audio-mixer/resources/tone.tres";

    public override async void _Ready()
    {
        // Set up the channels in the Inspector. The layout is applied here: the project setting would apply to every scene.
        AudioMixerServer.ApplyLayout(GD.Load<AudioMixerLayout>("res://audio-mixer/resources/mixer_layout.tres"));
        AudioMixerServer.Play("music", Tone);
        GD.Print("Music playing: ", AudioMixerServer.IsPlaying("music"));

        // Set up the channels in code.
        var music = new AudioMixerChannel
        {
            ChannelName = "music",
            Bus = "Music",
            DefaultFadeTime = 2.0f,
            QueueMode = AudioMixerChannel.QueueModeEnum.LoopQueue,
        };
        var sfx = new AudioMixerChannel { ChannelName = "sfx", Bus = "Sfx", MaxPolyphony = 8 };
        var layout = new AudioMixerLayout();
        layout.AddChannel(music);
        layout.AddChannel(sfx);
        AudioMixerServer.ApplyLayout(layout);
        GD.Print(string.Join(", ", AudioMixerServer.GetChannelNames()));

        // Show the title of the current track (connected first, so that the playlist below shows its title).
        AudioMixerServer.Singleton.ChannelStarted += OnChannelStarted;

        // Play a playlist.
        AudioMixerServer.Enqueue("music", Tone);
        AudioMixerServer.Enqueue("music", Tone);
        AudioMixerServer.Enqueue("music", Tone);
        AudioMixerServer.Play("music");
        AudioMixerServer.PlayNext("music", 0.5f);
        GD.Print("Queue index: ", AudioMixerServer.GetQueueIndex("music"));

        // Cross-fade to the boss music.
        AudioMixerServer.CrossFade("music", Tone, 3.0f);
        GD.Print("Music fading: ", AudioMixerServer.IsFading("music"));

        // Play a sound effect.
        AudioMixerServer.PlayOneShot("sfx", Tone, -3.0f, (float)GD.RandRange(0.9, 1.1));
        GD.Print("Sound effects: ", AudioMixerServer.GetVoiceCount("sfx"));

        // Keep the music in the pause menu.
        AudioMixerServer.AddChannel(new AudioMixerChannel
        {
            ChannelName = "voice",
            PauseWithTree = true,
            StopOnSceneChange = true,
        });
        GD.Print("Channels: ", string.Join(", ", AudioMixerServer.GetChannelNames()));

        // Lower the music while a voice speaks.
        AudioMixerServer.AddChannel(new AudioMixerChannel { ChannelName = "music", DuckOnTts = true, DuckVolumeDb = -15.0f });
        if (TtsServer.IsAvailable()) // no speech when headless
        {
            TtsServer.Speak("Welcome back!");
        }
        await PlayDialogLine(Tone);

        AudioMixerServer.Singleton.ChannelStarted -= OnChannelStarted;
        GetTree().Quit();
    }

    private void OnChannelStarted(StringName channel, long index, string streamPath)
    {
        if (channel == "music")
        {
            GetNode<Label>("NowPlaying").Text = streamPath.GetFile().GetBaseName();
            GD.Print("Now playing: ", GetNode<Label>("NowPlaying").Text);
        }
    }

    private async Task PlayDialogLine(string path)
    {
        AudioMixerServer.Duck("music", -10.0f);
        GD.Print("Music ducked: ", AudioMixerServer.IsDucked("music"));
        AudioMixerServer.Play("voice", path);
        await ToSignal(AudioMixerServer.Singleton, AudioMixerServerInstance.SignalName.ChannelFinished);
        AudioMixerServer.Unduck("music");
        GD.Print("Music ducked: ", AudioMixerServer.IsDucked("music"));
    }
}
