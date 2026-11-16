using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace DuckbillTweak.Plugin.Native;

internal static unsafe class NativeEmoteReader
{
    public static bool Available => EmoteController.Addresses.IsEmoting.Value != 0;

    /// <summary>Called on Framework with a currently valid object. Never retain the pointer across updates.</summary>
    public static ushort ReadActive(ICharacter actor)
    {
        if (!Available || actor.Address == 0) return 0;
        var native = (Character*)actor.Address;
        return native->EmoteController.IsEmoting() ? native->EmoteController.EmoteId : (ushort)0;
    }
}
