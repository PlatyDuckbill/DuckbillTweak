using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Use the tree list's own hidden-item layout; retain every original renderer and index.</summary>
internal sealed unsafe class NativeTreeFilter
{
    private readonly Dictionary<nint, bool> originalHidden = [];
    private bool collisionPending;

    public static int SourceCount(AtkComponentList* list)
    {
        if (list is null || list->GetComponentType() != ComponentType.TreeList) return -1;
        var tree = (AtkComponentTreeList*)list;
        return tree->Items.LongCount is >= 0 and <= 1000 && (tree->Items.LongCount == 0 || tree->Items.First is not null) ? tree->Items.Count : -1;
    }

    public static bool IsEnabled(AtkComponentList* list, int index)
    {
        var count = SourceCount(list);
        if (index < 0 || index >= count) return false;
        var item = ((AtkComponentTreeList*)list)->Items[index].Value;
        return item is not null && (item->State & TreeListItemState.Disabled) == 0;
    }

    public bool Apply(AtkUnitBase* addon, AtkComponentList* list, IReadOnlyList<bool> hide, out int visible)
    {
        visible = 0;
        if (SourceCount(list) != hide.Count) return false;
        var tree = (AtkComponentTreeList*)list;
        var live = new HashSet<nint>();
        for (var i = 0; i < hide.Count; i++)
        {
            var item = tree->Items[i].Value;
            if (item is null || !live.Add((nint)item)) return false;
        }
        foreach (var removed in originalHidden.Keys.Where(k => !live.Contains(k)).ToArray()) originalHidden.Remove(removed);
        var changed = false;
        for (var i = 0; i < hide.Count; i++)
        {
            var item = tree->Items[i].Value;
            if (!originalHidden.TryGetValue((nint)item, out var previous))
                originalHidden[(nint)item] = previous = item->IsHidden;
            var next = previous || hide[i];
            if (!next) visible++;
            if (item->IsHidden == next) continue;
            item->IsHidden = next;
            changed = true;
        }
        if (changed)
        {
            tree->LayoutRefreshPending = true;
            collisionPending = true;
        }
        FinishLayout(addon, list);
        return true;
    }

    public void FinishLayout(AtkUnitBase* addon, AtkComponentList* list)
    {
        if (!collisionPending || SourceCount(list) < 0 || ((AtkComponentTreeList*)list)->LayoutRefreshPending) return;
        addon->UpdateCollisionNodeList(false);
        collisionPending = false;
    }

    public void Restore(AtkUnitBase* addon, AtkComponentList* list)
    {
        if (originalHidden.Count > 0 && SourceCount(list) is var count && count >= 0)
        {
            var tree = (AtkComponentTreeList*)list;
            var changed = false;
            for (var i = 0; i < count; i++)
            {
                var item = tree->Items[i].Value;
                if (item is null || !originalHidden.TryGetValue((nint)item, out var hidden) || item->IsHidden == hidden) continue;
                item->IsHidden = hidden;
                changed = true;
            }
            if (changed) { tree->LayoutRefreshPending = true; collisionPending = true; }
        }
        originalHidden.Clear();
        FinishLayout(addon, list);
    }
}
