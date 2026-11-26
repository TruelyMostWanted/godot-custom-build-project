using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue285FeatureProgressBarServer;

/// <summary>C# checks of 285-feature-progress-bar-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue285FeatureProgressBarServerCheck : FeatureCheckCs
{
    private const int BarCount = 1000;
    private const int BenchFrames = 60;

    protected override string CheckName => "285-feature-progress-bar-server";

    private static ProgressBarStyle MakeStyle()
    {
        return new ProgressBarStyle
        {
            Background = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.6f) },
            Fill = new StyleBoxFlat { BgColor = Colors.Green },
        };
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    protected override async Task RunChecks()
    {
        Expect(Engine.HasSingleton("ProgressBarServer"), "ProgressBarServer is a registered singleton");
        Expect(ClassDB.ClassExists("ProgressBarStyle"), "ProgressBarStyle is registered");

        // Style defaults.
        var defaults = new ProgressBarStyle();
        ExpectEqual(defaults.Size, new Vector2(32, 4), "default size");
        ExpectEqual(defaults.Offset, new Vector2(0, -20), "default offset");
        ExpectEqual(defaults.FillMode, ProgressBar.FillModeEnum.BeginToEnd, "default fill_mode");
        ExpectEqual(defaults.FollowMode, ProgressBarStyle.FollowModeEnum.Inherit, "default follow_mode");

        int baseCount = ProgressBarServer.GetBarCount();
        ProgressBarStyle style = MakeStyle();

        // Single bar: ratio, clamping, setters.
        var unit = new Node2D();
        AddChild(unit);
        Rid bar = ProgressBarServer.BarCreate(unit, style);
        Expect(bar.IsValid, "BarCreate returns a valid RID");
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount + 1, "bar count grows");
        Expect(Mathf.IsEqualApprox(ProgressBarServer.BarGetRatio(bar), 1.0f), "new bars are full");
        ProgressBarServer.BarSetRatio(bar, 0.25f);
        Expect(Mathf.IsEqualApprox(ProgressBarServer.BarGetRatio(bar), 0.25f), "BarSetRatio");
        ProgressBarServer.BarSetRatio(bar, 5.0f);
        Expect(Mathf.IsEqualApprox(ProgressBarServer.BarGetRatio(bar), 1.0f), "ratio is clamped to 1");
        ProgressBarServer.BarSetOffset(bar, new Vector2(0, -8));
        ProgressBarServer.BarSetModulate(bar, Colors.Red);
        ProgressBarServer.BarSetVisible(bar, false);
        ProgressBarServer.BarSetStyle(bar, new ProgressBarStyle()); // Falls back to the ProgressBar theme.
        style.FillMode = ProgressBar.FillModeEnum.BottomToTop; // Redraws all bars of the style.

        // Position-only bars on a rotated unit: updated once per frame (only when drawing), no errors.
        ProgressBarStyle upright = MakeStyle();
        upright.FollowMode = ProgressBarStyle.FollowModeEnum.PositionOnly;
        ProgressBarServer.BarSetStyle(bar, upright);
        unit.Rotation = Mathf.Pi / 2;
        await NextFrame();
        await NextFrame();

        ProgressBarServer.BarFree(bar);
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount, "BarFree frees the bar");
        unit.QueueFree();

        // Freeing a unit frees its bars (no leaked RIDs).
        var doomed = new Node2D();
        AddChild(doomed);
        ProgressBarServer.BarCreate(doomed, style);
        ProgressBarServer.BarCreate(doomed, style);
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount + 2, "two bars on one unit");
        doomed.QueueFree();
        await NextFrame();
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount, "deleting the unit frees its bars");

        // A unit removed from the tree but kept alive keeps its bars until it is freed.
        var pooled = new Node2D();
        AddChild(pooled);
        ProgressBarServer.BarCreate(pooled, style);
        RemoveChild(pooled);
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount + 1, "a unit outside the tree keeps its bars");
        pooled.Free();
        await NextFrame();
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount, "a unit freed outside the tree frees its bars");

        await Benchmark(style, baseCount);
    }

    /// <summary>1000 bars with values changing every frame: CPU time of the bulk update per frame.</summary>
    private async Task Benchmark(ProgressBarStyle style, int baseCount)
    {
        var units = new List<Node2D>(BarCount);
        var bars = new Godot.Collections.Array<Rid>();
        for (int i = 0; i < BarCount; i++)
        {
            var u = new Node2D { Position = new Vector2(i % 40, i / 40) * 24.0f };
            AddChild(u);
            units.Add(u);
            bars.Add(ProgressBarServer.BarCreate(u, style));
        }
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount + BarCount, $"{BarCount} bars created");

        var ratios = new float[BarCount];
        ulong totalUsec = 0;
        for (int frame = 0; frame < BenchFrames; frame++)
        {
            for (int i = 0; i < BarCount; i++)
            {
                ratios[i] = (i * 0.01f + frame * 0.05f) % 1.0f;
            }
            ulong start = Time.GetTicksUsec();
            ProgressBarServer.BarsSetRatios(bars, ratios);
            totalUsec += Time.GetTicksUsec() - start;
            await NextFrame();
        }
        Expect((float)ProgressBarServer.BarGetRatio(bars[10]) == ratios[10], "BarsSetRatios sets every bar");
        GD.Print($"BENCH cs: {BarCount} bars, BarsSetRatios {totalUsec / 1000.0 / BenchFrames:F3} ms per frame (average of {BenchFrames} frames, {OS.GetName()})");

        foreach (Node2D u in units)
        {
            u.QueueFree();
        }
        await NextFrame();
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), baseCount, "freeing 1000 units frees all their bars");
    }
}
