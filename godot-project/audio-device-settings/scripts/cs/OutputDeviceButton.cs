using Godot;

namespace TruelyInteractive.AudioDeviceSettingsGuide;

public partial class OutputDeviceButton : OptionButton
{
    public override void _Ready()
    {
        // Android, iOS and the Web only have "Default": the system or browser picks the device.
        Disabled = !AudioDeviceSettingsServer.IsDeviceSelectionSupported();
        foreach (string device in AudioServer.GetOutputDeviceList())
        {
            AddItem(device);
        }
        SelectDevice(AudioDeviceSettingsServer.GetOutputDevice());
        ItemSelected += OnItemSelected;
        AudioDeviceSettingsServer.Singleton.OutputDeviceChanged += SelectDevice;
    }

    private void OnItemSelected(long index)
    {
        AudioDeviceSettingsServer.SetOutputDevice(GetItemText((int)index));
        AudioDeviceSettingsServer.Singleton.Save();
    }

    private void SelectDevice(string device)
    {
        // Also called when a reset or a loaded file changed the device.
        for (int i = 0; i < ItemCount; i++)
        {
            if (GetItemText(i) == device)
            {
                Select(i); // Select() doesn't emit ItemSelected: no loop
                return;
            }
        }
    }
}
