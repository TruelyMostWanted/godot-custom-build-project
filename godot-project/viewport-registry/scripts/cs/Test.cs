using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ViewportRegistry;

/// <summary>Full API test of Viewport Registry (ViewportServer and the ViewportTarget of the graphics settings servers).
/// Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const int UnknownWindowId = 123456;

    private readonly List<Viewport> _added = [];
    private readonly List<Viewport> _removed = [];
    // Instance IDs: a freed Window's C# wrapper is no longer the same object as the signal argument.
    private readonly List<ulong> _removedIds = [];

    private Window _root;
    private SubViewport _early;

    protected override Task RunTests()
    {
        _root = GetTree().Root;
        TestSingleton();
        TestInitialScan();
        TestWindows();
        TestLookups();
        TestSignals();
        TestFreeEarlyViewport();
        TestViewportTargetDefaults();
        TestViewportTargetModes();
        return Task.CompletedTask;
    }

    private void TestSingleton()
    {
        Expect(Engine.HasSingleton("ViewportServer"), "ViewportServer is a registered singleton");
    }

    // This runs in _Ready(), before the first frame: the viewport enters the tree before the first call.
    private void TestInitialScan()
    {
        _early = new SubViewport();
        AddChild(_early);
        Godot.Collections.Array<Viewport> viewports = ViewportServer.GetViewports();
        Expect(viewports.Count >= 2, "GetViewports lists at least the root and the early viewport");
        Expect(viewports[0] == _root, "the root window comes first");
        Expect(viewports.Contains(_early), "GetViewports has the viewport added before the first call");
        ExpectEqual(ViewportServer.GetViewportCount(), viewports.Count, "GetViewportCount matches GetViewports");
        Expect(ViewportServer.HasViewport(_root), "HasViewport(root)");
        Expect(ViewportServer.HasViewport(_early), "HasViewport(early)");
    }

    private void TestWindows()
    {
        Godot.Collections.Array<Window> windows = ViewportServer.GetWindows();
        Expect(windows.Contains(_root), "GetWindows has the root window");
        ExpectEqual(windows[0], _root, "the root window comes first");
        Expect(!windows.Any(window => (Viewport)window == _early), "GetWindows has no SubViewport");
    }

    private void TestLookups()
    {
        Expect(ViewportServer.GetViewportByRid(_early.GetViewportRid()) == _early, "GetViewportByRid finds the SubViewport");
        Expect(ViewportServer.GetViewportByRid(_root.GetViewportRid()) == _root, "GetViewportByRid finds the root");
        Expect(ViewportServer.GetViewportByRid(new Rid()) == null, "GetViewportByRid(new Rid()) is null");
        Expect(ViewportServer.GetWindowById((int)DisplayServer.MainWindowId) == _root, "GetWindowById(MainWindowId) is the root");
        Expect(ViewportServer.GetWindowById((int)DisplayServer.InvalidWindowId) == null, "GetWindowById(InvalidWindowId) is null");
        Expect(ViewportServer.GetWindowById(UnknownWindowId) == null, "GetWindowById of an unknown ID is null");
    }

    private void TestSignals()
    {
        ViewportServer.ViewportAdded += OnViewportAdded;
        ViewportServer.ViewportRemoved += OnViewportRemoved;
        int count = ViewportServer.GetViewportCount();

        var holder = new Node();
        var nested = new SubViewport();
        holder.AddChild(nested);
        AddChild(holder);
        Expect(_added.Count == 1 && _added[0] == nested, "ViewportAdded for a viewport inside an added subtree");
        ExpectEqual(ViewportServer.GetViewportCount(), count + 1, "the count grows by one");

        var plain = new Node();
        AddChild(plain);
        plain.Free();
        ExpectEqual(_added.Count, 1, "no ViewportAdded for a plain Node");
        ExpectEqual(_removed.Count, 0, "no ViewportRemoved for a plain Node");

        var window = new Window();
        AddChild(window);
        Expect(_added.Contains(window), "ViewportAdded for a Window");
        Expect(ViewportServer.GetWindows().Contains(window), "GetWindows has the new Window");
        Expect(ViewportServer.GetViewportByRid(window.GetViewportRid()) == window, "GetViewportByRid finds the Window");
        // The headless display server gives every native window ID 0, the ID of the root: GetWindowById(0) is the root.
        Expect(ViewportServer.GetWindowById(window.GetWindowId()) != null, "GetWindowById finds a window for the ID of the new Window");

        RemoveChild(holder);
        Expect(_removed.Count == 1 && _removed[0] == nested, "ViewportRemoved when the subtree leaves the tree");
        Expect(!ViewportServer.HasViewport(nested), "HasViewport is false after removal");
        holder.Free();

        ulong windowId = window.GetInstanceId();
        window.Free();
        Expect(_removedIds.Contains(windowId), "ViewportRemoved when the Window is freed");
        ViewportServer.ViewportAdded -= OnViewportAdded;
        ViewportServer.ViewportRemoved -= OnViewportRemoved;
    }

    private void TestFreeEarlyViewport()
    {
        int count = ViewportServer.GetViewportCount();
        _early.Free();
        ExpectEqual(ViewportServer.GetViewportCount(), count - 1, "the count shrinks by one after freeing a viewport");
    }

    private void TestViewportTargetDefaults()
    {
        ExpectEqual(TextureSettingsServer.ViewportTarget, SettingsServer.ViewportTargetMode.Group, "TextureSettingsServer.ViewportTarget defaults to Group");
        ExpectEqual(RenderQualitySettingsServer.ViewportTarget, SettingsServer.ViewportTargetMode.Group, "RenderQualitySettingsServer.ViewportTarget defaults to Group");
        ExpectEqual(ShadowSettingsServer.ViewportTarget, SettingsServer.ViewportTargetMode.Group, "ShadowSettingsServer.ViewportTarget defaults to Group");
    }

    private void TestViewportTargetModes()
    {
        bool snap = !_root.Snap2DTransformsToPixel;
        var outside = new SubViewport { Snap2DTransformsToPixel = !snap };
        AddChild(outside);
        // A session override is not stored in the player's settings file.
        TextureSettingsServer.Singleton.SetSessionOverride("snap_2d_transforms_to_pixel", snap);
        ExpectEqual(_root.Snap2DTransformsToPixel, snap, "the root gets the value");
        ExpectEqual(outside.Snap2DTransformsToPixel, !snap, "Group: a viewport outside the group keeps its value");

        TextureSettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.AllViewports;
        ExpectEqual(TextureSettingsServer.ViewportTarget, SettingsServer.ViewportTargetMode.AllViewports, "ViewportTarget is AllViewports");
        ExpectEqual(outside.Snap2DTransformsToPixel, snap, "AllViewports: every registered viewport gets the value at once");
        var late = new SubViewport { Snap2DTransformsToPixel = !snap };
        AddChild(late);
        ExpectEqual(late.Snap2DTransformsToPixel, snap, "AllViewports: a viewport that enters the tree later gets the value");

        TextureSettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.RootOnly;
        TextureSettingsServer.Singleton.ClearSessionOverride("snap_2d_transforms_to_pixel");
        ExpectEqual(_root.Snap2DTransformsToPixel, !snap, "the root gets the stored value back");
        ExpectEqual(late.Snap2DTransformsToPixel, snap, "RootOnly: the other viewports keep their values");
        TextureSettingsServer.ViewportTarget = SettingsServer.ViewportTargetMode.Group;
        late.Free();
        outside.Free();
    }

    private void OnViewportAdded(Viewport viewport)
    {
        _added.Add(viewport);
    }

    private void OnViewportRemoved(Viewport viewport)
    {
        _removed.Add(viewport);
        _removedIds.Add(viewport.GetInstanceId());
    }
}
