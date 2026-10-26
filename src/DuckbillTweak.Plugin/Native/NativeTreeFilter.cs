using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Compact flat shop rows without replacing renderers or changing source indices.</summary>
internal sealed unsafe class NativeTreeFilter
{
    private sealed record SavedRow(ushort Height, bool Hidden, bool Disabled, nint Renderer, bool Visible);
    private readonly Dictionary<nint, SavedRow> saved = [];

    public static int SourceCount(AtkComponentList* list)
    {
        if (list is null || list->GetComponentType() != ComponentType.TreeList) return -1;
        var tree = (AtkComponentTreeList*)list;
        return tree->Items.LongCount is >= 0 and <= 1000 && (tree->Items.LongCount == 0 || tree->Items.First is not null) ? tree->Items.Count : -1;
    }

    public bool IsEnabled(AtkComponentList* list, int index)
    {
        var count = SourceCount(list);
        if (index < 0 || index >= count) return false;
        var item = ((AtkComponentTreeList*)list)->Items[index].Value;
        return item is not null && (saved.TryGetValue((nint)item, out var previous)
            ? !previous.Disabled : (item->State & TreeListItemState.Disabled) == 0);
    }

    public bool Apply(AtkUnitBase* addon, AtkComponentList* list, IReadOnlyList<bool> hide, out int visible)
    {
        visible = 0;
        if (SourceCount(list) != hide.Count) return false;
        var tree = (AtkComponentTreeList*)list;
        var live = new HashSet<nint>();
        var renderers = new HashSet<nint>();
        // This contract is for flat shop rows only. Never reinterpret a grouped tree.
        for (var i = 0; i < hide.Count; i++)
        {
            var item = tree->Items[i].Value;
            if (item is null || !live.Add((nint)item) || item->Depth != 0 ||
                (item->Type & (TreeListItemType.Group | TreeListItemType.SectionHeader)) != 0 ||
                item->Renderer is null || item->Renderer->OwnerNode is null || !renderers.Add((nint)item->Renderer)) return false;
        }
        foreach (var removed in saved.Keys.Where(k => !live.Contains(k)).ToArray()) saved.Remove(removed);
        var changed = false;
        for (var i = 0; i < hide.Count; i++)
        {
            var item = tree->Items[i].Value;
            var node = item->Renderer->OwnerNode;
            if (!hide[i])
            {
                if (saved.Remove((nint)item, out var previous)) { RestoreRow(item, previous); changed = true; }
                if (!item->IsHidden) visible++;
                continue;
            }
            if (!saved.ContainsKey((nint)item))
            {
                saved[(nint)item] = new(item->Height, item->IsHidden, (item->State & TreeListItemState.Disabled) != 0, (nint)item->Renderer,
                    (node->NodeFlags & NodeFlags.Visible) != 0);
                changed = true;
            }
            changed |= item->Height != 0 || !item->IsHidden || (node->NodeFlags & NodeFlags.Visible) != 0;
            // KR layout/draw advance by Height, independently of IsHidden. Both are required.
            item->Height = 0;
            item->IsHidden = true;
            item->State |= TreeListItemState.Disabled;
            node->ToggleVisibility(false);
        }
        if (changed) Relayout(addon, tree);
        return true;
    }

    public void FinishLayout(AtkUnitBase* addon, AtkComponentList* list)
    {
        if (saved.Count == 0 || SourceCount(list) < 0) return;
        var tree = (AtkComponentTreeList*)list;
        var changed = false;
        // A native refresh can make a renderer visible again before our next snapshot.
        for (var i = 0; i < tree->Items.Count; i++)
        {
            var item = tree->Items[i].Value;
            if (item is null || !saved.TryGetValue((nint)item, out var previous) || item->Renderer is null || item->Renderer->OwnerNode is null) continue;
            var node = item->Renderer->OwnerNode;
            if (previous.Renderer != (nint)item->Renderer)
                saved[(nint)item] = previous with { Renderer = (nint)item->Renderer, Visible = (node->NodeFlags & NodeFlags.Visible) != 0 };
            if ((node->NodeFlags & NodeFlags.Visible) == 0) continue;
            node->ToggleVisibility(false);
            changed = true;
        }
        if (changed) addon->UpdateCollisionNodeList(false);
    }

    private static void RestoreRow(AtkComponentTreeListItem* item, SavedRow previous)
    {
        item->Height = previous.Height;
        item->IsHidden = previous.Hidden;
        item->State = previous.Disabled ? item->State | TreeListItemState.Disabled : item->State & ~TreeListItemState.Disabled;
        if ((nint)item->Renderer == previous.Renderer && item->Renderer is not null && item->Renderer->OwnerNode is not null)
            item->Renderer->OwnerNode->ToggleVisibility(previous.Visible);
    }

    private static void Relayout(AtkUnitBase* addon, AtkComponentTreeList* tree)
    {
        // Local KR disassembly confirms these recalculate height, clamp scroll and position
        // existing renderers. No item event, selection or purchase callback is dispatched.
        tree->CalculateVisibleItemHeightAndCount(&tree->VisibleItemHeight, &tree->VisibleItemCount);
        tree->RecalculateVisibleItems(false);
        addon->UpdateCollisionNodeList(false);
    }

    public void Restore(AtkUnitBase* addon, AtkComponentList* list)
    {
        if (saved.Count > 0 && SourceCount(list) is var count && count >= 0)
        {
            var tree = (AtkComponentTreeList*)list;
            var changed = false;
            for (var i = 0; i < count; i++)
            {
                var item = tree->Items[i].Value;
                if (item is null || !saved.TryGetValue((nint)item, out var previous)) continue;
                RestoreRow(item, previous);
                changed = true;
            }
            if (changed) Relayout(addon, tree);
        }
        saved.Clear();
    }
}
