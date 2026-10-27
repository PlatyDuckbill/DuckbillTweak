namespace DuckbillTweak.Plugin.Native;

// Curated complete distributions. Layout/IL evidence and fixed sources: docs/NATIVE-UI.md.
// No version ranges, independent module allowlists, or runtime self-approval.
internal static class NativeRuntimeProfiles
{
    internal const string GameSha256 = "DB9824EFAC6D5370AB64B02353E882931DDA447E160EE1A5D9E5DCBA43695728";

    private static readonly NativeRuntimeProfile[] Profiles =
    [
        new("15.0.3.9-wasdcxz",
            new(new("1e8f635a-5365-40f6-b920-830d6a816d43"), "7255103B62A1BB3687492B78E8FAF4B91BD969826B39D76602D0A861C92E158D"),
            new(new("93c1ca2b-80f3-43ef-9d58-ab0e99bb8909"), "052AC3B331F6DFC2A72B58D61506A1A696A68EB13416B3C3D514FAFDF5EB4F40"),
            new(new("a296f580-78c4-4617-afef-e85be4bdfe55"), "04C4C92CD05506EDFED442EEE9D331734AF7B9E151A572964365B80EF38B9422")),
        new("15.0.3.7-dal4kr",
            new(new("b4e06917-bffd-4e12-89da-82bde6b4c73e"), "B92AC6B5BB51BB874590EB2E5D4BCDD1F9328786AB3D8A5D493583EFFD447926"),
            new(new("3b9613ec-ea8d-4d0e-9c1e-6f91f3b46d50"), "0E9F985538F800F75B4AABADE1C443F3537D93C695BE368852CA44D9A7D3371E"),
            new(new("d5357f8d-0f92-464f-8936-de6fe0816b4d"), "A75408D83A54D2ED59B3BF178A0474772CE86F4046F2645E76C2B84AE232B178")),
    ];

    internal static NativeRuntimeProfile? Match(NativeModuleIdentity dalamud, NativeModuleIdentity bindings, NativeModuleIdentity interop) =>
        Profiles.SingleOrDefault(profile => profile.Dalamud == dalamud && profile.Bindings == bindings && profile.Interop == interop);
}

internal sealed record NativeRuntimeProfile(string Id, NativeModuleIdentity Dalamud, NativeModuleIdentity Bindings, NativeModuleIdentity Interop);
internal sealed record NativeModuleIdentity(Guid Mvid, string Sha256);
