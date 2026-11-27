using Godot;

namespace TruelyInteractive.InputSettingsGuide;

public partial class SensitivitySlider : HSlider
{
    public override void _Ready()
    {
        MinValue = 0.1;
        MaxValue = 5.0;
        Step = 0.1;
        Value = InputSettingsServer.GetSensitivity(InputSettingsServer.Device.Mouse); // before connecting: no signal
        ValueChanged += OnValueChanged;
        DragEnded += OnDragEnded;
        InputSettingsServer.Singleton.MouseSensitivityChanged += OnMouseSensitivityChanged;
    }

    private void OnValueChanged(double value)
    {
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Mouse, (float)value);
    }

    private void OnDragEnded(bool valueChanged)
    {
        InputSettingsServer.Singleton.Save();
    }

    private void OnMouseSensitivityChanged(double sensitivity)
    {
        // A reset or a loaded file changed the sensitivity.
        SetValueNoSignal(sensitivity); // no ValueChanged, no loop
    }
}
