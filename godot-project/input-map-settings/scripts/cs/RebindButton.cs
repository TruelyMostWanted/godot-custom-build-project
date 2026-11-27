using Godot;

namespace TruelyInteractive.InputMapSettings;

public partial class RebindButton : Button
{
    /// <summary>The action this button rebinds and the column it shows.</summary>
    [Export] public StringName Action { get; set; } = "jump";
    [Export] public InputActionRebinder.DeviceFilterEnum DeviceFilter { get; set; } = InputActionRebinder.DeviceFilterEnum.KeyboardMouse;
    [Export] public int EventIndex { get; set; }
    /// <summary>Seconds without input until the button gives up.</summary>
    [Export] public double Timeout { get; set; } = 5.0;

    public readonly InputActionRebinder Rebinder = new();
    private SceneTreeTimer _timer;

    public override void _Ready()
    {
        Rebinder.DeviceFilter = DeviceFilter;
        Rebinder.ConflictMode = InputActionRebinder.ConflictModeEnum.Swap;
        Rebinder.Started += OnStarted;
        Rebinder.Canceled += (action, index) => OnFinished();
        Rebinder.Completed += (action, index, @event) => OnFinished();
        Pressed += () => Rebinder.Begin(Action, EventIndex);
        // Also fires for the other action of a swap, a reset and a loaded file.
        InputMapSettingsServer.ActionEventsChanged += OnActionEventsChanged;
        UpdateText();
    }

    public override void _ExitTree()
    {
        InputMapSettingsServer.ActionEventsChanged -= OnActionEventsChanged;
    }

    public override void _Input(InputEvent @event)
    {
        // While the button waits, the input belongs to it: no other control or action sees it.
        if (Rebinder.Update(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnStarted(StringName action, long index)
    {
        Text = "Press a key...";
        SceneTreeTimer timer = GetTree().CreateTimer(Timeout);
        _timer = timer;
        timer.Timeout += () =>
        {
            if (_timer == timer)
            {
                Rebinder.Cancel();
            }
        };
    }

    private void OnFinished()
    {
        _timer = null;
        UpdateText();
        InputMapSettingsServer.Singleton.Save();
    }

    private void OnActionEventsChanged(StringName changed)
    {
        if (changed == Action && !Rebinder.IsActive())
        {
            UpdateText();
        }
    }

    private void UpdateText()
    {
        InputEvent ev = EventInColumn();
        Text = ev != null ? InputMapSettingsServer.GetEventLabel(ev) : "-";
    }

    private InputEvent EventInColumn()
    {
        // The EventIndex-th event of the action in this column, counted like the rebinder counts.
        int index = 0;
        foreach (InputEvent ev in InputMapSettingsServer.GetEvents(Action))
        {
            bool isPad = InputAction.GetEventDevice(ev) == InputAction.InputDevice.Gamepad;
            if (DeviceFilter == InputActionRebinder.DeviceFilterEnum.KeyboardMouse && isPad)
            {
                continue;
            }
            if (DeviceFilter == InputActionRebinder.DeviceFilterEnum.Gamepad && !isPad)
            {
                continue;
            }
            if (index == EventIndex)
            {
                return ev;
            }
            index++;
        }
        return null;
    }
}
