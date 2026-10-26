using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes.ComponentNode;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Keep source indices intact while compacting the game's pooled row renderers.</summary>
internal sealed unsafe class NativeTreeFilter
{
    private sealed record SavedRow(nint Item, ushort Height, bool Hidden, bool Disabled, nint Renderer, int Pool);
    private readonly List<SavedRow> saved = [];
    private readonly List<nint[]> pools = [];
    private readonly Dictionary<nint, bool> visibility = [];
    private bool[] hidden = [];
    private EmptyRenderer? empty;
    public string Failure { get; private set; } = "";
    public int RendererCount => visibility.Count;

    // Native Draw visits zero-height rows even when IsHidden suppresses population.
    // Hidden rows need a blank renderer; borrowing a visible renderer draws it twice.
    private sealed class EmptyRenderer : ComponentNode<AtkComponentListItemRenderer, AtkUldComponentDataListItemRenderer>
    {
        public EmptyRenderer()
        {
            SetInternalComponentType(ComponentType.ListItemRenderer);
            Size = new(0, 0);
            IsVisible = false;
            CollisionNode.NodeFlags = 0;
            NodeFlags = 0;
            Component->ListItemIndex = -1;
            Component->ComponentFlags = 0;
        }
    }

    public static int SourceCount(AtkComponentList* list)
    {
        if (list is null || list->GetComponentType() != ComponentType.TreeList) return -1;
        var tree = (AtkComponentTreeList*)list;
        return tree->Items.LongCount is >= 0 and <= 1000 && (tree->Items.LongCount == 0 || tree->Items.First is not null) ? tree->Items.Count : -1;
    }

    public bool IsEnabled(AtkComponentList* list, int index)
    {
        if (index < 0 || index >= SourceCount(list)) return false;
        var item = ((AtkComponentTreeList*)list)->Items[index].Value;
        return item is not null && (index < saved.Count && saved[index].Item == (nint)item
            ? !saved[index].Disabled : (item->State & TreeListItemState.Disabled) == 0);
    }

    public bool Apply(AtkUnitBase* addon, AtkComponentList* list, IReadOnlyList<bool> hide, out int visible)
    {
        visible = hide.Count(x => !x);
        Failure = "";
        if (SourceCount(list) != hide.Count) { Failure = "source count mismatch"; return false; }
        var tree = (AtkComponentTreeList*)list;
        if (!hide.Any(x => x)) { Restore(addon, list); return true; }
        if (Matches(tree) && hidden.SequenceEqual(hide)) { FinishLayout(addon, list); return true; }
        Restore(addon, list);

        // Inspected KR SetupItem assigns renderers modulo (NodeCount + 1).
        var rendererPool = new Dictionary<nint, int>();
        if (list->NumCategoryRenderers is 0 or > 32 || list->CategoryItemRendererList is null)
        { Failure = "missing native renderer pools"; return false; }
        for (var p = 0; p < list->NumCategoryRenderers; p++)
        {
            ref var category = ref list->CategoryItemRendererList[p];
            if (category.NodeCount > 1000 || category.AtkComponentListItemRenderer is null ||
                (category.NodeCount > 0 && category.NodeList is null))
            { Failure = $"invalid renderer pool {p}"; Clear(); return false; }
            var renderers = new nint[category.NodeCount + 1];
            renderers[0] = (nint)category.AtkComponentListItemRenderer;
            for (var j = 0; j < category.NodeCount; j++)
            {
                var node = &category.NodeList[j];
                if ((ushort)node->Type < 1000 || node->Component is null || node->Component->GetComponentType() != ComponentType.ListItemRenderer)
                { Failure = $"invalid renderer pool {p} node {j}"; Clear(); return false; }
                renderers[j + 1] = (nint)node->Component;
            }
            foreach (var address in renderers)
            {
                var renderer = (AtkComponentListItemRenderer*)address;
                if (renderer->OwnerNode is null || !rendererPool.TryAdd(address, p))
                { Failure = $"duplicate/invalid pool {p}"; Clear(); return false; }
                visibility[address] = (renderer->OwnerNode->NodeFlags & NodeFlags.Visible) != 0;
            }
            pools.Add(renderers);
        }
        var items = new HashSet<nint>();
        for (var i = 0; i < hide.Count; i++)
        {
            var item = tree->Items[i].Value;
            if (item is null || !items.Add((nint)item) || item->Depth != 0 ||
                (item->Type & (TreeListItemType.Group | TreeListItemType.LastItemInGroup)) != 0 ||
                !rendererPool.TryGetValue((nint)item->Renderer, out var pool))
            { Failure = $"row {i}: unsupported tree/pool; depth={((item is null) ? -1 : item->Depth)}; type={((item is null) ? -1 : (byte)item->Type)}"; Clear(); return false; }
            saved.Add(new((nint)item, item->Height, item->IsHidden, (item->State & TreeListItemState.Disabled) != 0, (nint)item->Renderer, pool));
        }
        hidden = hide.ToArray();
        // No two rows intersecting a viewport may use the same pooled renderer.
        // Include the native draw loop's extra row at either viewport boundary.
        var lastBottom = new Dictionary<nint, int>();
        var nextSlot = new int[pools.Count];
        var top = 0;
        for (var i = 0; i < saved.Count; i++)
        {
            if (hidden[i]) continue;
            var row = saved[i];
            var pool = pools[row.Pool];
            var renderer = pool[nextSlot[row.Pool]++ % pool.Length];
            if (lastBottom.TryGetValue(renderer, out var bottom) && top - bottom <= list->OwnerNode->Height)
            { Failure = $"renderer pool {row.Pool} too small for viewport"; Clear(); return false; }
            lastBottom[renderer] = top + row.Height;
            top += row.Height;
        }
        empty = new EmptyRenderer();
        empty.AttachNode(addon);
        Assign(tree);
        Relayout(addon, tree);
        FinishLayout(addon, list);
        return true;
    }

    private bool Matches(AtkComponentTreeList* tree)
    {
        if (saved.Count == 0 || tree->Items.Count != saved.Count) return false;
        for (var i = 0; i < saved.Count; i++) if ((nint)tree->Items[i].Value != saved[i].Item) return false;
        return true;
    }

    private bool Assign(AtkComponentTreeList* tree)
    {
        var next = new int[pools.Count];
        var changed = false;
        for (var i = 0; i < saved.Count; i++)
        {
            var item = tree->Items[i].Value;
            var previous = saved[i];
            var pool = pools[previous.Pool];
            var renderer = hidden[i] ? empty!.Component : (AtkComponentListItemRenderer*)pool[next[previous.Pool]++ % pool.Length];
            var height = hidden[i] ? (ushort)0 : previous.Height;
            var isHidden = hidden[i] || previous.Hidden;
            changed |= item->Renderer != renderer || item->Height != height || item->IsHidden != isHidden;
            item->Renderer = renderer;
            item->Height = height;
            item->IsHidden = isHidden;
            item->State = hidden[i] || previous.Disabled ? item->State | TreeListItemState.Disabled : item->State & ~TreeListItemState.Disabled;
        }
        return changed;
    }

    public void FinishLayout(AtkUnitBase* addon, AtkComponentList* list)
    {
        if (empty is null || SourceCount(list) < 0) return;
        var tree = (AtkComponentTreeList*)list;
        if (!Matches(tree)) { Restore(addon, list); return; }
        if (Assign(tree)) Relayout(addon, tree);
        // Hide leftover pooled nodes, never a renderer used in the current viewport.
        var active = new HashSet<nint>();
        var y = -list->ScrollOffset;
        for (var i = 0; i < saved.Count; i++)
        {
            var item = tree->Items[i].Value;
            if (!hidden[i] && y + item->Height >= 0 && y <= list->OwnerNode->Height) active.Add((nint)item->Renderer);
            y += item->Height;
        }
        var changed = false;
        foreach (var address in visibility.Keys)
        {
            var node = ((AtkComponentListItemRenderer*)address)->OwnerNode;
            var show = active.Contains(address);
            if (((node->NodeFlags & NodeFlags.Visible) != 0) == show) continue;
            node->ToggleVisibility(show);
            changed = true;
        }
        if (changed) addon->UpdateCollisionNodeList(false);
    }

    private static void Relayout(AtkUnitBase* addon, AtkComponentTreeList* tree)
    {
        // Population still receives the original source index. Quantity, preview and
        // purchase events identify the original item, never its filtered position.
        tree->CalculateVisibleItemHeightAndCount(&tree->VisibleItemHeight, &tree->VisibleItemCount);
        tree->RecalculateVisibleItems(false);
        addon->UpdateCollisionNodeList(false);
    }

    public void Restore(AtkUnitBase* addon, AtkComponentList* list)
    {
        if (saved.Count > 0 && SourceCount(list) >= 0)
        {
            var tree = (AtkComponentTreeList*)list;
            // PreRefresh restores before native arrays are rebuilt. Also restore any
            // surviving items if a native update unexpectedly changed the vector.
            var previousRows = saved.ToDictionary(x => x.Item);
            var restored = false;
            for (var i = 0; i < tree->Items.Count; i++)
            {
                var item = tree->Items[i].Value;
                if (item is null || !previousRows.TryGetValue((nint)item, out var previous)) continue;
                item->Renderer = (AtkComponentListItemRenderer*)previous.Renderer;
                item->Height = previous.Height;
                item->IsHidden = previous.Hidden;
                item->State = previous.Disabled ? item->State | TreeListItemState.Disabled : item->State & ~TreeListItemState.Disabled;
                restored = true;
            }
            if (restored)
            {
                foreach (var (address, show) in visibility) ((AtkComponentListItemRenderer*)address)->OwnerNode->ToggleVisibility(show);
                Relayout(addon, tree);
            }
        }
        Clear();
    }

    private void Clear()
    {
        saved.Clear(); pools.Clear(); visibility.Clear(); hidden = [];
        empty?.Dispose(); empty = null;
    }
}
