using Godot;

namespace TruelyInteractive.WorldSpaceProgressBars;

/// <summary>Minimal example of World Space Progress Bars: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Fill directions
        var bar = new ProgressBar
        {
            FillMode = (int)ProgressBar.FillModeEnum.BottomToTop,
            CustomMinimumSize = new Vector2(12, 80),
            ShowPercentage = false,
            Value = 40,
        };
        GD.Print("Vertical bar value: ", bar.Value);

        // A separate look for vertical bars
        var theme = new Theme();
        theme.SetTypeVariation("VProgressBar", "ProgressBar");
        theme.SetStylebox("fill", "VProgressBar", new StyleBoxFlat { BgColor = Colors.Orange });
        bar.Theme = theme;
        bar.ThemeTypeVariation = "VProgressBar";
        GD.Print("Type variation: ", bar.ThemeTypeVariation);
        bar.Free();

        // A health bar over a unit
        var unit = new Node2D();
        AddChild(unit);
        var unitBar = new ProgressBar();
        unitBar.ShowPercentage = false;
        unitBar.Size = new Vector2(32, 4);
        unitBar.Position = new Vector2(-16, -24);
        unitBar.MouseFilter = Control.MouseFilterEnum.Ignore;
        unit.AddChild(unitBar);
        unitBar.MaxValue = 100.0;
        unitBar.Value = 75.0;
        GD.Print("Control bar over the unit: ", unitBar.Value, " / ", unitBar.MaxValue);

        // Give a unit a health bar
        var barStyle = GD.Load<ProgressBarStyle>("res://world-space-progress-bars/resources/health_bar.tres");
        float maxHealth = 100.0f;
        float health = 100.0f;
        Rid healthBar = ProgressBarServer.BarCreate(unit, barStyle);
        health = Mathf.Max(health - 30.0f, 0.0f);
        ProgressBarServer.BarSetRatio(healthBar, health / maxHealth);
        GD.Print("Server bar ratio after 30 damage: ", ProgressBarServer.BarGetRatio(healthBar));

        // Make a style
        var style = new ProgressBarStyle
        {
            Size = new Vector2(40, 5),
            Offset = new Vector2(0, -32),
            Fill = new StyleBoxFlat { BgColor = Colors.Green },
            HideWhenFull = true,
        };
        ProgressBarServer.BarSetStyle(healthBar, style);
        GD.Print("Style size: ", style.Size);

        // Keep bars upright on rotating units
        style.FollowMode = ProgressBarStyle.FollowModeEnum.PositionOnly;
        GD.Print("Follow mode: ", style.FollowMode);

        // Update thousands of bars at once
        Rid manaBar = ProgressBarServer.BarCreate(unit, style);
        ProgressBarServer.BarSetOffset(manaBar, new Vector2(0, 6));
        Godot.Collections.Array<Rid> bars = [healthBar, manaBar];
        float[] ratios = [0.5f, 0.25f];
        ProgressBarServer.BarsSetRatios(bars, ratios);
        GD.Print("Bars: ", ProgressBarServer.GetBarCount(), ", mana ratio: ", ProgressBarServer.BarGetRatio(manaBar));

        ProgressBarServer.BarFree(healthBar);
        ProgressBarServer.BarFree(manaBar);
        GetTree().Quit();
    }
}
