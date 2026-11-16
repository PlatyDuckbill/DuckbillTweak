using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace DuckbillTweak.Plugin.Native;

internal static unsafe class NativeEmoteReader
{
    /// <summary>Called on Framework with a currently valid object. Never retain the pointer across updates.</summary>
    public static ushort ReadLoop(ICharacter actor)
    {
        if (actor.Address == 0) return 0;
        var native = (Character*)actor.Address;
        return native->EmoteController.IsInEmoteLoop() ? native->EmoteController.EmoteId : (ushort)0;
    }
}
