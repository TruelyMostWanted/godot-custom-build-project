using Godot;

namespace TruelyInteractive.SettingsServers;

[GlobalClass]
public partial class GameSettingsResourceCs : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")] public double MusicVolume { get; set; } = 0.8;
    [Export(PropertyHint.Enum, "Easy,Normal,Hard")] public int Difficulty { get; set; } = 1;
    [Export] public bool ShowHints { get; set; } = true;
    [Export] public int MaxFps { get; set; }
}
