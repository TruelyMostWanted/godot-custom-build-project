using Godot;

namespace TruelyInteractive.AudioBusSettingsGuide;

public partial class VolumeSlider : HSlider
{
    /// <summary>Name of the bus in the bus layout.</summary>
    [Export] public StringName Bus { get; set; } = "Master";

    public override void _Ready()
    {
        MinValue = 0.0;
        MaxValue = 1.0;
        Step = 0.01;
        Value = AudioBusSettingsServer.GetBusVolumeLinear(Bus); // before connecting: no signal
        ValueChanged += OnValueChanged;
        DragEnded += OnDragEnded;
        AudioBusSettingsServer.Singleton.BusVolumeChanged += OnBusVolumeChanged;
    }

    private void OnValueChanged(double value)
    {
        AudioBusSettingsServer.SetBusVolumeLinear(Bus, (float)value);
    }

    private void OnDragEnded(bool valueChanged)
    {
        AudioBusSettingsServer.Singleton.Save();
    }

    private void OnBusVolumeChanged(StringName busName, double volumeDb)
    {
        // Another slider, a reset or a loaded file changed the volume.
        if (busName == Bus)
        {
            SetValueNoSignal(Mathf.DbToLinear(volumeDb)); // no ValueChanged, no loop
        }
    }
}
