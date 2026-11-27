using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.WorldSpaceProgressBars;

/// <summary>Full API test of World Space Progress Bars (ProgressBarServer, ProgressBarStyle). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const int BenchBars = 1000;
    private const int BenchFrames = 60;

    private readonly ProgressBarStyle _style = new();
    private int _baseCount;

    protected override async Task RunTests()
    {
        _baseCount = ProgressBarServer.GetBarCount();
        TestStyleDefaults();
        TestStyleProperties();
        TestRatio();
        TestBarSetters();
        await TestPositionOnly();
        await TestUnitFreed();
        await TestUnitOutsideTree();
        await TestBarsSetRatios();
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void TestStyleDefaults()
    {
        var defaults = new ProgressBarStyle();
        ExpectEqual(defaults.Size, new Vector2(32, 4), "default Size is 32x4");
        ExpectEqual(defaults.Offset, new Vector2(0, -20), "default Offset is (0, -20)");
        ExpectEqual(defaults.Background, null, "default Background is empty");
        ExpectEqual(defaults.Fill, null, "default Fill is empty");
        ExpectEqual(defaults.FillMode, ProgressBar.FillModeEnum.BeginToEnd, "default FillMode is begin to end");
        ExpectEqual(defaults.FollowMode, ProgressBarStyle.FollowModeEnum.Inherit, "default FollowMode is inherit");
        ExpectEqual(defaults.HideWhenEmpty, false, "default HideWhenEmpty is false");
        ExpectEqual(defaults.HideWhenFull, false, "default HideWhenFull is false");
        ExpectEqual((int)ProgressBarStyle.FollowModeEnum.PositionOnly, 1, "FollowModeEnum.PositionOnly is 1");
        ExpectEqual((int)ProgressBarStyle.FollowModeEnum.Max, 2, "FollowModeEnum.Max is 2");
    }

    private void TestStyleProperties()
    {
        var background = new StyleBoxFlat();
        var fill = new StyleBoxFlat();
        _style.Size = new Vector2(40, 5);
        _style.Offset = new Vector2(0, -32);
        _style.Background = background;
        _style.Fill = fill;
        _style.FillMode = ProgressBar.FillModeEnum.BottomToTop;
        _style.HideWhenEmpty = true;
        _style.HideWhenFull = true;
        ExpectEqual(_style.Size, new Vector2(40, 5), "Size is stored");
        ExpectEqual(_style.Offset, new Vector2(0, -32), "Offset is stored");
        ExpectEqual(_style.Background, background, "Background is stored");
        ExpectEqual(_style.Fill, fill, "Fill is stored");
        ExpectEqual(_style.FillMode, ProgressBar.FillModeEnum.BottomToTop, "FillMode is stored");
        ExpectEqual(_style.HideWhenEmpty, true, "HideWhenEmpty is stored");
        ExpectEqual(_style.HideWhenFull, true, "HideWhenFull is stored");
    }

    private void TestRatio()
    {
        var unit = new Node2D();
        AddChild(unit);
        Rid bar = ProgressBarServer.BarCreate(unit, _style);
        Expect(bar.IsValid, "BarCreate returns a valid RID");
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount + 1, "GetBarCount counts the new bar");
        ExpectEqual(ProgressBarServer.BarGetRatio(bar), 1.0, "a new bar is full");
        ProgressBarServer.BarSetRatio(bar, 0.25f);
        ExpectEqual(ProgressBarServer.BarGetRatio(bar), 0.25, "BarSetRatio sets the ratio");
        ProgressBarServer.BarSetRatio(bar, 5.0f);
        ExpectEqual(ProgressBarServer.BarGetRatio(bar), 1.0, "a ratio above 1 is clamped to 1");
        ProgressBarServer.BarSetRatio(bar, -1.0f);
        ExpectEqual(ProgressBarServer.BarGetRatio(bar), 0.0, "a ratio below 0 is clamped to 0");
        ProgressBarServer.BarFree(bar);
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount, "BarFree frees the bar");
        unit.Free();
    }

    private void TestBarSetters()
    {
        var unit = new Node2D();
        AddChild(unit);
        Rid bar = ProgressBarServer.BarCreate(unit, _style);
        ProgressBarServer.BarSetRatio(bar, 0.5f);
        ProgressBarServer.BarSetOffset(bar, new Vector2(0, 6));
        ProgressBarServer.BarSetModulate(bar, Colors.Red);
        ProgressBarServer.BarSetVisible(bar, false);
        var themeStyle = new ProgressBarStyle();
        ProgressBarServer.BarSetStyle(bar, themeStyle);
        _style.FillMode = ProgressBar.FillModeEnum.TopToBottom;
        ExpectEqual(ProgressBarServer.BarGetRatio(bar), 0.5, "offset, modulate, visible and style keep the ratio");
        ProgressBarServer.BarFree(bar);
        unit.Free();
    }

    private async Task TestPositionOnly()
    {
        var upright = new ProgressBarStyle();
        upright.FollowMode = ProgressBarStyle.FollowModeEnum.PositionOnly;
        ExpectEqual(upright.FollowMode, ProgressBarStyle.FollowModeEnum.PositionOnly, "FollowMode is stored");
        var unit = new Node2D();
        AddChild(unit);
        Rid bar = ProgressBarServer.BarCreate(unit, upright);
        unit.Rotation = Mathf.Pi / 2;
        await NextFrame();
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount + 1, "a position-only bar on a rotated unit lives on");
        ProgressBarServer.BarFree(bar);
        unit.Free();
    }

    private async Task TestUnitFreed()
    {
        var unit = new Node2D();
        AddChild(unit);
        ProgressBarServer.BarCreate(unit, _style);
        ProgressBarServer.BarCreate(unit, _style);
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount + 2, "two bars on one unit");
        unit.QueueFree();
        await NextFrame();
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount, "deleting the unit frees its bars");
    }

    private async Task TestUnitOutsideTree()
    {
        var pooled = new Node2D();
        AddChild(pooled);
        ProgressBarServer.BarCreate(pooled, _style);
        RemoveChild(pooled);
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount + 1, "a unit outside the tree keeps its bars");
        pooled.Free();
        await NextFrame();
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount, "a unit freed outside the tree frees its bars");
    }

    /// <summary>Also the benchmark of the wiki: 1000 bars, every value changes every frame.</summary>
    private async Task TestBarsSetRatios()
    {
        var units = new List<Node2D>(BenchBars);
        var bars = new Godot.Collections.Array<Rid>();
        for (int i = 0; i < BenchBars; i++)
        {
            var unit = new Node2D();
            AddChild(unit);
            units.Add(unit);
            Rid bar = ProgressBarServer.BarCreate(unit, _style);
            bars.Add(bar);
        }
        var ratios = new float[BenchBars];
        ulong totalUsec = 0;
        for (int frame = 0; frame < BenchFrames; frame++)
        {
            for (int i = 0; i < BenchBars; i++)
            {
                ratios[i] = (i * 0.01f + frame * 0.05f) % 1.0f;
            }
            ulong start = Time.GetTicksUsec();
            ProgressBarServer.BarsSetRatios(bars, ratios);
            totalUsec += Time.GetTicksUsec() - start;
            await NextFrame();
        }
        ExpectEqual((float)ProgressBarServer.BarGetRatio(bars[10]), ratios[10], "BarsSetRatios sets every bar");
        GD.Print($"BENCH cs: {BenchBars} bars, BarsSetRatios {totalUsec / 1000.0 / BenchFrames:F3} ms per frame");
        foreach (Node2D unit in units)
        {
            unit.QueueFree();
        }
        await NextFrame();
        await NextFrame();
        ExpectEqual(ProgressBarServer.GetBarCount(), _baseCount, "freeing 1000 units frees all their bars");
    }
}
