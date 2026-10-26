using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Suppress native hit nodes while a filtered list occupies the same space.</summary>
internal sealed unsafe class NativeListInput
{
    private const NodeFlags MouseFlags = NodeFlags.HasCollision | NodeFlags.RespondToMouse;
    private readonly Dictionary<nint, NodeFlags> saved = [];

    public bool Suppress(AtkComponentList* list, out bool changed)
    {
        changed = false;
        if (!TryNodes(list, out var nodes)) return false;
        foreach (var obsolete in saved.Keys.Where(k => !nodes.Contains(k)).ToArray()) saved.Remove(obsolete);
        foreach (var address in nodes)
        {
            var node = (AtkResNode*)address;
            if (!saved.ContainsKey(address)) saved[address] = node->NodeFlags & MouseFlags;
            changed |= (node->NodeFlags & MouseFlags) != 0;
            node->NodeFlags &= ~MouseFlags;
        }
        return true;
    }

    public void Restore(AtkComponentList* list)
    {
        if (saved.Count == 0) return;
        // Only dereference nodes that still belong to this live component tree.
        if (TryNodes(list, out var nodes))
            foreach (var address in nodes)
                if (saved.TryGetValue(address, out var flags))
                {
                    var node = (AtkResNode*)address;
                    node->NodeFlags = (node->NodeFlags & ~MouseFlags) | flags;
                }
        saved.Clear();
    }

    private static bool TryNodes(AtkComponentList* list, out HashSet<nint> nodes)
    {
        nodes = [];
        if (list is null || list->OwnerNode is null) return false;
        if (list->ListLength is < 0 or > 1000) return false;
        var components = new Queue<nint>();
        var visited = new HashSet<nint>();
        nodes.Add((nint)list->OwnerNode);
        components.Enqueue((nint)list);
        for (var i = 0; i < list->ListLength; i++)
        {
            var renderer = list->GetItemRenderer(i);
            if (renderer is null) continue;
            if (renderer->OwnerNode is not null) nodes.Add((nint)renderer->OwnerNode);
            components.Enqueue((nint)renderer);
        }
        while (components.TryDequeue(out var address))
        {
            if (!visited.Add(address)) continue;
            if (visited.Count > 1024) return false;
            var component = (AtkComponentBase*)address;
            var uld = &component->UldManager;
            if (uld->NodeListCount > 4096 || (uld->NodeListCount > 0 && uld->NodeList is null)) return false;
            for (var i = 0; i < uld->NodeListCount; i++)
            {
                var node = uld->NodeList[i];
                if (node is null || !nodes.Add((nint)node)) continue;
                if (nodes.Count > 16384) return false;
                if ((int)node->Type < 1000) continue;
                var nested = ((AtkComponentNode*)node)->Component;
                if (nested is not null) components.Enqueue((nint)nested);
            }
        }
        return true;
    }
}
