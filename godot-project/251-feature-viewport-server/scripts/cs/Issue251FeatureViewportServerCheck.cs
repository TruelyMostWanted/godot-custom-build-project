using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue251FeatureViewportServer;

/// <summary>C# checks of 251-feature-viewport-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue251FeatureViewportServerCheck : FeatureCheckCs
{
    private readonly List<Viewport> _added = new();
    private readonly List<Viewport> _removed = new();
    // Instance IDs: a freed Window's C# wrapper is no longer the same object as the signal argument.
    private readonly List<ulong> _removedIds = new();

    protected override string CheckName => "251-feature-viewport-server";

    private void OnViewportAdded(Viewport viewport) => _added.Add(viewport);

    private void OnViewportRemoved(Viewport viewport)
    {
        _removed.Add(viewport);
        _removedIds.Add(viewport.GetInstanceId());
    }

    protected override Task RunChecks()
    {
        Expect(Engine.HasSingleton("ViewportServer"), "ViewportServer is a registered singleton");
        Window root = GetTree().Root;

        // Initial scan: the root window and a viewport that entered the tree before the first call
        // (this runs in _Ready(), before the first frame).
        var early = new SubViewport();
        AddChild(early);
        Godot.Collections.Array<Viewport> viewports = ViewportServer.GetViewports();
        Expect(viewports.Count >= 2, "GetViewports() lists at least the root and the early viewport");
        Expect(viewports.Count > 0 && viewports[0] == root, "the root window comes first");
        Expect(viewports.Contains(early), "GetViewports() has the viewport added before the first call");
        ExpectEqual(ViewportServer.GetViewportCount(), viewports.Count, "GetViewportCount() matches GetViewports()");
        Expect(ViewportServer.HasViewport(root), "HasViewport(root)");
        Expect(ViewportServer.HasViewport(early), "HasViewport(early)");

        Godot.Collections.Array<Window> windows = ViewportServer.GetWindows();
        Expect(windows.Contains(root), "GetWindows() has the root window");
        Expect(!windows.Any(w => (Viewport)w == early), "GetWindows() has no SubViewport");

        // Lookups.
        Expect(ViewportServer.GetViewportByRid(early.GetViewportRid()) == early, "GetViewportByRid() finds the SubViewport");
        Expect(ViewportServer.GetViewportByRid(root.GetViewportRid()) == root, "GetViewportByRid() finds the root");
        Expect(ViewportServer.GetViewportByRid(new Rid()) == null, "GetViewportByRid(new Rid()) is null");
        Expect(ViewportServer.GetWindowById((int)DisplayServer.MainWindowId) == root, "GetWindowById(MainWindowId) is the root");
        Expect(ViewportServer.GetWindowById((int)DisplayServer.InvalidWindowId) == null, "GetWindowById(InvalidWindowId) is null");
        Expect(ViewportServer.GetWindowById(123456) == null, "GetWindowById() of an unknown ID is null");

        // Signals.
        ViewportServer.ViewportAdded += OnViewportAdded;
        ViewportServer.ViewportRemoved += OnViewportRemoved;
        int count = ViewportServer.GetViewportCount();

        var holder = new Node();
        var nested = new SubViewport();
        holder.AddChild(nested);
        AddChild(holder);
        Expect(_added.Count == 1 && _added[0] == nested, "ViewportAdded for a viewport inside an added subtree");
        ExpectEqual(ViewportServer.GetViewportCount(), count + 1, "count grows by one");

        var plain = new Node();
        AddChild(plain);
        plain.Free();
        ExpectEqual(_added.Count, 1, "no ViewportAdded for a plain Node");
        ExpectEqual(_removed.Count, 0, "no ViewportRemoved for a plain Node");

        var window = new Window();
        AddChild(window);
        Expect(_added.Contains(window), "ViewportAdded for a Window");
        Expect(ViewportServer.GetWindows().Contains(window), "GetWindows() has the new Window");
        Expect(ViewportServer.GetViewportByRid(window.GetViewportRid()) == window, "GetViewportByRid() finds the Window");
        // The headless display server gives every native window ID 0, the ID of the root.
        if (!window.IsEmbedded() && window.GetWindowId() > DisplayServer.MainWindowId)
        {
            Expect(ViewportServer.GetWindowById(window.GetWindowId()) == window, "GetWindowById() finds the native Window");
        }

        RemoveChild(holder);
        Expect(_removed.Count == 1 && _removed[0] == nested, "ViewportRemoved when the subtree leaves the tree");
        Expect(!ViewportServer.HasViewport(nested), "HasViewport() is false after removal");
        holder.Free();

        ulong windowId = window.GetInstanceId();
        window.Free();
        Expect(_removedIds.Contains(windowId), "ViewportRemoved when the Window is freed");
        early.Free();
        ExpectEqual(ViewportServer.GetViewportCount(), count - 1, "count after freeing the Window and the early viewport");

        ViewportServer.ViewportAdded -= OnViewportAdded;
        ViewportServer.ViewportRemoved -= OnViewportRemoved;
        return Task.CompletedTask;
    }
}
