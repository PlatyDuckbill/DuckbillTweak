using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System.Text.Json;
using DuckbillTweak.Core.Shops;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Lumina.Data.Files;

if (args.Length >= 3 && args[0] == "native-contract")
{
    const string expectedGame = "DB9824EFAC6D5370AB64B02353E882931DDA447E160EE1A5D9E5DCBA43695728";
    var exe = File.ReadAllBytes(args[1]);
    if (Convert.ToHexString(SHA256.HashData(exe)) != expectedGame) throw new InvalidDataException("Game hash mismatch.");
    using var gamePe = new PEReader(new MemoryStream(exe));
    foreach (var (rva, bytes) in new (int, byte[])[]
    {
        (0xCD5682, [0x40, 0xB6, 0x01]),
        (0xCD568A, [0xE8, 0xE1, 0xC6, 0xB6, 0xFF]),
        (0xCD571E, [0x40, 0xB6, 0x01]),
    })
    {
        var section = gamePe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva + bytes.Length <= s.VirtualAddress + s.SizeOfRawData);
        var offset = section.PointerToRawData + rva - section.VirtualAddress;
        if (!exe.AsSpan(offset, bytes.Length).SequenceEqual(bytes)) throw new InvalidDataException($"Instruction mismatch at {rva:X}.");
        Console.WriteLine($"RVA={rva:X}; Original={Convert.ToHexString(bytes)}; Match=True");
    }
    using var bindings = File.OpenRead(args[2]);
    using var bindingsPe = new PEReader(bindings);
    var metadata = bindingsPe.GetMetadataReader();
    var module = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    if (module != new Guid("93c1ca2b-80f3-43ef-9d58-ab0e99bb8909")) throw new InvalidDataException("Client binding module mismatch.");
    Console.WriteLine($"GameSHA256={expectedGame}; BindingMVID={module}; Match=True");
    return 0;
}

// Inspect the actual artifact without loading the plugin or invoking any game services.
if (args.Length >= 2 && args[0] == "resources")
{
    var expectedPath = args.Length > 2 ? args[2] : "data/special-currencies.kr.json";
    using var assembly = File.OpenRead(args[1]);
    using var pe = new PEReader(assembly);
    var metadata = pe.GetMetadataReader();
    var matches = metadata.ManifestResources.Select(metadata.GetManifestResource)
        .Where(r => metadata.GetString(r.Name) == "DuckbillTweak.SpecialCurrencies").ToArray();
    if (matches.Length != 1 || !matches[0].Implementation.IsNil)
        throw new InvalidDataException("Currency data must be embedded in the main plugin DLL exactly once.");
    var resource = matches[0];
    var rva = checked(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress + (int)resource.Offset);
    var reader = pe.GetSectionData(rva).GetReader();
    var payload = reader.ReadBytes(reader.ReadInt32());
    var expected = File.ReadAllBytes(expectedPath);
    if (!SHA256.HashData(payload).AsSpan().SequenceEqual(SHA256.HashData(expected)))
        throw new InvalidDataException("Embedded currency data differs from the source JSON.");
    using var json = JsonDocument.Parse(payload);
    if (json.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
        throw new InvalidDataException("Unexpected currency schema.");
    var artifactDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
    foreach (var reference in metadata.AssemblyReferences.Select(metadata.GetAssemblyReference))
    {
        var name = metadata.GetString(reference.Name);
        if (name is not ("DuckbillTweak.Core" or "KamiToolKit")) continue;
        if (!File.Exists(Path.Combine(artifactDirectory, name + ".dll")))
            throw new InvalidDataException($"Required plugin dependency missing: {name}.dll");
        if (name == "KamiToolKit" && !File.Exists(Path.Combine(artifactDirectory, "KamiToolKit.LICENSE.txt")))
            throw new InvalidDataException("KamiToolKit license notice missing.");
    }
    Console.WriteLine($"EmbeddedResource=DuckbillTweak.SpecialCurrencies; Bytes={payload.Length}; SourceMatch=True; Tokens={json.RootElement.GetProperty("items").EnumerateObject().Count()}");
    return 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("GameDataProbe <sqpack path> logs|shops|audit|kornago-audit\nGameDataProbe <sqpack path> items|npc <name>\nGameDataProbe resources <plugin DLL path> [currency json path]");
    return 1;
}
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.Korean });
if (args[1] == "items" && args.Length >= 3)
{
    foreach (var item in game.GetExcelSheet<Item>()!.Where(i => i.Name.ExtractText().Contains(args[2], StringComparison.Ordinal)))
        Console.WriteLine($"Item={item.RowId}; Name={item.Name.ExtractText()}; Category={item.ItemUICategory.RowId}/{item.ItemUICategory.ValueNullable?.Name.ExtractText()}; Search={item.ItemSearchCategory.RowId}; Action={item.ItemAction.RowId}/{item.ItemAction.ValueNullable?.Action.RowId}; ActionData={string.Join(',', item.ItemAction.ValueNullable?.Data.ToArray() ?? [])}; Additional={item.AdditionalData.RowType?.Name}/{item.AdditionalData.RowId}; Equip={item.EquipSlotCategory.RowId}");
}
else if (args[1] == "consumables-audit")
{
    foreach (var group in game.GetExcelSheet<Item>()!.Where(i => !i.Name.IsEmpty && i.ItemAction.ValueNullable?.Action.RowId is 816 or 944)
                 .GroupBy(i => i.ItemAction.Value.Action.RowId))
    {
        Console.WriteLine($"Action={group.Key}; Items={group.Count()}; Equipped={group.Count(i => i.EquipSlotCategory.RowId != 0)}");
        foreach (var item in group) Console.WriteLine($"Item={item.RowId}; Category={item.ItemUICategory.RowId}; Name={item.Name.ExtractText()}");
    }
}
else if (args[1] == "npc" && args.Length >= 3)
{
    foreach (var npc in game.GetExcelSheet<ENpcResident>()!.Where(n => n.Singular.ExtractText().Contains(args[2], StringComparison.Ordinal)))
    {
        var basis = game.GetExcelSheet<ENpcBase>()!.GetRow(npc.RowId);
        Console.WriteLine($"Npc={npc.RowId}; Name={npc.Singular.ExtractText()}; References={string.Join(',', basis.ENpcData.Select(r => $"{r.RowType?.Name}/{r.RowId}"))}");
    }
}
else if (args[1] == "kornago-audit")
{
    foreach (var shop in game.GetExcelSheet<SpecialShop>()!.Where(s => s.RowId is 1771019 or 1771020 or 1771036))
    {
        Console.WriteLine($"Shop={shop.RowId}; Name={shop.Name.ExtractText()}; Quest={shop.Quest.RowId}; Festival={shop.RequiredFestival.RowId}; Content={shop.RequiredContentFinderCondition.RowId}");
        foreach (var row in shop.Item.Where(r => r.ReceiveItems.Any(i => i.Item.RowId != 0)).Take(2))
            Console.WriteLine($"Reward={string.Join(',', row.ReceiveItems.Where(r => r.Item.RowId != 0).Select(r => $"{r.Item.RowId}/{r.Item.Value.Name.ExtractText()}x{r.ReceiveCount}"))}; Costs={string.Join(',', row.ItemCosts.Where(c => c.ItemCost.RowId != 0).Select(c => $"{c.ItemCost.RowId}x{c.CurrencyCost}/type{c.CostType}"))}; Quest={row.Quest.RowId}; Achievement={row.AchievementUnlock.RowId}");
    }
    foreach (var category in game.GetExcelSheet<ItemUICategory>()!.Where(c => c.RowId is >= 42 and <= 60))
        Console.WriteLine($"Category={category.RowId}/{category.Name.ExtractText()}");
}
else if (args[1] == "ownership-audit")
{
    foreach (var id in new uint[] { 14273, 14274 })
    {
        var faded = game.GetExcelSheet<Item>()!.GetRow(id);
        var results = game.GetExcelSheet<Recipe>()!.Where(r => r.Ingredient.Any(i => i.RowId == id) &&
            r.ItemResult.IsValid && r.ItemResult.Value.AdditionalData.Is<Orchestrion>()).Select(r => r.ItemResult.Value).DistinctBy(i => i.RowId).ToArray();
        Console.WriteLine($"Faded={id}/{faded.Name.ExtractText()}; Results={string.Join(',', results.Select(i => $"{i.RowId}/{i.Name.ExtractText()}/Orchestrion{i.AdditionalData.RowId}"))}");
        if (results.Length == 0) throw new InvalidDataException("Expected faded-roll recipe connection missing.");
    }
    var cabinet = game.GetExcelSheet<Cabinet>()!.Where(c => c.Item.RowId == 24337).ToArray();
    Console.WriteLine($"Horn=24337; CabinetRows={string.Join(',', cabinet.Select(c => c.RowId))}; MaximumCabinetRow={game.GetExcelSheet<Cabinet>()!.Max(c => c.RowId)}");
    foreach (var set in game.GetExcelSheet<MirageStoreSetItem>()!.Where(s => s.Head.RowId == 24337))
        Console.WriteLine($"HornOutfit={set.RowId}; Name={game.GetExcelSheet<Item>()!.GetRow(set.RowId).Name.ExtractText()}");
}
else if (args[1] == "logs")
{
    foreach (var row in game.GetExcelSheet<LogMessage>()!)
    {
        var text = row.Text.ExtractText();
        if (text.Contains("주사위") || text.Contains("입찰") || row.RowId is >= 1220 and <= 1238 or >= 5170 and <= 5200)
            Console.WriteLine($"{row.RowId}: {row.Text.ToMacroString()}");
    }
}
else if (args[1] == "shops")
{
    foreach (var group in game.GetExcelSheet<SpecialShop>()!.GroupBy(x => x.UseCurrencyType))
    {
        Console.WriteLine($"CurrencyType={group.Key}; Shops={group.Count()}");
        foreach (var shop in group.Take(3))
        {
            Console.WriteLine($" {shop.RowId}: {shop.Name}");
            foreach (var entry in shop.Item.Where(x => x.ReceiveItems.Any(i => i.Item.RowId != 0)).Take(2))
                Console.WriteLine($"  Receive={string.Join(',', entry.ReceiveItems.Select(x => $"{x.Item.RowId}x{x.ReceiveCount}"))}; Cost={string.Join(',', entry.ItemCosts.Select(x => $"{x.ItemCost.RowId}x{x.CurrencyCost}/type{x.CostType}/collect{x.CollectabilityCost}"))}");
        }
    }
}
else if (args[1] == "audit")
{
    using var mapping = JsonDocument.Parse(File.ReadAllText(args.Length > 2 ? args[2] : "data/special-currencies.kr.json"));
    var tokens = mapping.RootElement.GetProperty("items").EnumerateObject().ToDictionary(p => uint.Parse(p.Name), p => p.Value.GetUInt32());
    var items = game.GetExcelSheet<Item>()!;
    foreach (var (token, itemId) in tokens)
        Console.WriteLine($"Token {token} -> Item {itemId}: {items.GetRow(itemId).Name.ExtractText()}");
    var tomes = game.GetExcelSheet<TomestonesItem>()!.Where(t => t.Tomestones.RowId != 0 && t.Item.RowId != 0)
        .GroupBy(t => t.Tomestones.RowId).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Item.RowId);
    var resolver = new SpecialCurrencyResolver(tokens, tomes);
    foreach (var (index, itemId) in tomes)
        Console.WriteLine($"Tomestone {index} -> Item {itemId}: {items.GetRow(itemId).Name.ExtractText()}");
    foreach (var kind in new byte[] { 0, 1, 2, 3 })
    {
        var examples = game.GetExcelSheet<SpecialShop>()!.SelectMany(s => s.Item.SelectMany(o => o.ItemCosts
            .Where(c => c.ItemCost.RowId != 0 && c.CostType == kind && c.ItemCost.RowId <= 28)
            .Select(c => $"{s.RowId}/{s.Name.ExtractText()}: Type={kind}, Raw={c.ItemCost.RowId}, Amount={c.CurrencyCost}")))
            .Distinct().Take(3);
        foreach (var example in examples) Console.WriteLine(example);
    }
    var unknown = new Dictionary<string, int>();
    var resolved = 0;
    foreach (var shop in game.GetExcelSheet<SpecialShop>()!)
    foreach (var offer in shop.Item.Where(e => e.ReceiveItems.Any(r => r.Item.RowId != 0)))
    foreach (var cost in offer.ItemCosts.Where(c => c.ItemCost.RowId != 0))
    {
        var id = resolver.Resolve(cost.CostType, cost.ItemCost.RowId);
        if (id is { } value && items.TryGetRow(value, out var row) && !row.Name.IsEmpty) resolved++;
        else
        {
            var key = $"Encoding={shop.UseCurrencyType};CostType={cost.CostType};Id={cost.ItemCost.RowId}";
            unknown[key] = unknown.GetValueOrDefault(key) + 1;
        }
    }
    Console.WriteLine($"Resolved prices={resolved}; Unknown prices={unknown.Values.Sum()}");
    foreach (var group in unknown.OrderByDescending(p => p.Value).Take(20)) Console.WriteLine($" {group.Key}; Count={group.Value}");
}
else if (args[1] == "uld" && args.Length >= 3)
{
    var uld = game.GetFile<UldFile>(args[2]) ?? throw new InvalidDataException("ULD not found.");
    Console.WriteLine($"{args[2]}: Theme={uld.WidgetData.AssetsHaveThemeSupport}; Nodes={uld.WidgetData.NodeCount}");
    foreach (var node in uld.WidgetData.Nodes)
        Console.WriteLine($"Node={node.NodeId}; Type={node.NodeType}; Parent={node.ParentId}; X={node.X}; Y={node.Y}; W={node.W}; H={node.H}; Data={node.Node}");
    foreach (var component in uld.Components)
        Console.WriteLine($"Component={component.Id}; Type={component.Type}; Nodes={component.NodeCount}");
}
else return 1;
return 0;
