using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System.Text.Json;
using DuckbillTweak.Core.Shops;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Lumina.Data.Files;
using DuckbillTweak.Plugin.Native;

if (args.Length >= 3 && args[0] == "native-contract")
{
    // Runs the plugin's own per-feature code contracts (NativeCode) against an executable file. No game hash gate.
    var exe = File.ReadAllBytes(args[1]);
    var reader = new FileCodeReader(exe);
    Console.WriteLine($"Game={args[1]}; SHA256={Convert.ToHexString(SHA256.HashData(exe))}; Gate=None");
    var pvp = NativeCode.ResolvePvpBubbles(reader);
    var exchange = NativeCode.ResolveExchangeCurrency(reader);
    foreach (var patch in pvp.Value ?? [])
        Console.WriteLine($"PvpBubbles: RVA={reader.Rva(patch.Address):X}; Original={Convert.ToHexString(patch.Original)}; Replacement={Convert.ToHexString(patch.Replacement)}");
    Console.WriteLine($"Contract=PvpBubbles; Available={pvp.Available}; {pvp.Reason}");
    Console.WriteLine($"Contract=ShopExchangeCurrency; Available={exchange.Available}; {exchange.Reason}");
    // Report-only analysis basis for features that use installed ClientStructs functions (not a runtime contract).
    foreach (var (name, pattern) in new[]
    {
        ("CollisionDrawFlagsBit21", "8B 8B B0 00 00 00 C1 E9 15"),
        ("SetAddonOrderAfterArguments", "41 0F B6 F1 49 8B D8 48 8B EA"),
        ("SetAddonDepthLayerUnlink", "41 80 E2 EF 49 8D 49 30 48 8B D3"),
    })
        Console.WriteLine($"Basis={name}; Matches={string.Join(",", reader.ScanAll(pattern).Select(a => reader.Rva(a).ToString("X")))}; Gate=None");
    if (!pvp.Available || !exchange.Available) throw new InvalidDataException("A native code contract failed.");
    // Report-only: module identities are no longer a runtime gate (see NativeEnvironment / docs/NATIVE-UI.md).
    // Member/signature differences between distributions are reviewed with scripts/compare-native-runtime.ps1.
    var referenceDirectory = Directory.Exists(args[2]) ? args[2] : Path.GetDirectoryName(Path.GetFullPath(args[2]))!;
    foreach (var name in new[] { "Dalamud.dll", "FFXIVClientStructs.dll", "InteropGenerator.Runtime.dll" })
    {
        using var stream = File.OpenRead(Path.Combine(referenceDirectory, name));
        var sha256 = Convert.ToHexString(SHA256.HashData(stream));
        stream.Position = 0;
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
        Console.WriteLine($"Module={name}; MVID={mvid}; SHA256={sha256}; Gate=None");
    }
    Console.WriteLine("CodeContracts=Match; ModuleGate=None");
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
    // Inspect both actual DLLs: a low plugin reference cannot compensate for a stale
    // UI library built against a higher runtime. Do not load either assembly.
    foreach (var artifact in new[] { args[1], Path.Combine(artifactDirectory, "KamiToolKit.dll") })
    {
        using var artifactStream = File.OpenRead(artifact);
        using var artifactPe = new PEReader(artifactStream);
        var artifactMetadata = artifactPe.GetMetadataReader();
        foreach (var reference in artifactMetadata.AssemblyReferences.Select(artifactMetadata.GetAssemblyReference))
        {
            var name = artifactMetadata.GetString(reference.Name);
            var maximum = name switch
            {
                "Dalamud" => new Version(15, 0, 3, 7),
                "FFXIVClientStructs" => new Version(7, 57, 0, 9386),
                "InteropGenerator.Runtime" => new Version(1, 0, 0, 0),
                _ => null,
            };
            if (maximum is null) continue;
            if (reference.Version > maximum)
                throw new InvalidDataException($"{Path.GetFileName(artifact)} requires {name} {reference.Version}, above the shared compile reference {maximum}.");
            Console.WriteLine($"Artifact={Path.GetFileName(artifact)}; Reference={name}/{reference.Version}; SharedRuntimeReference=True");
        }
    }
    Console.WriteLine($"EmbeddedResource=DuckbillTweak.SpecialCurrencies; Bytes={payload.Length}; SourceMatch=True; Tokens={json.RootElement.GetProperty("items").EnumerateObject().Count()}");
    var historyResource = metadata.ManifestResources.Select(metadata.GetManifestResource)
        .Single(r => metadata.GetString(r.Name) == "DuckbillTweak.ReleaseHistory" && r.Implementation.IsNil);
    var historyRva = checked(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress + (int)historyResource.Offset);
    var historyReader = pe.GetSectionData(historyRva).GetReader();
    var historyPayload = historyReader.ReadBytes(historyReader.ReadInt32());
    if (!SHA256.HashData(historyPayload).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes("data/releases.json"))))
        throw new InvalidDataException("Embedded release history differs from source.");
    using var history = JsonDocument.Parse(historyPayload);
    var artifactVersion = metadata.GetAssemblyDefinition().Version.ToString(4);
    var currentNotes = history.RootElement.EnumerateArray().Where(entry => entry.GetProperty("Version").GetString() == artifactVersion).ToArray();
    if (currentNotes.Length != 1 || currentNotes[0].GetProperty("Korean").GetArrayLength() == 0 ||
        currentNotes[0].GetProperty("English").GetArrayLength() != currentNotes[0].GetProperty("Korean").GetArrayLength())
        throw new InvalidDataException("Release history must describe this artifact in both languages.");
    Console.WriteLine($"EmbeddedResource=DuckbillTweak.ReleaseHistory; SourceMatch=True; Version={artifactVersion}; Entries={currentNotes[0].GetProperty("Korean").GetArrayLength()}");
    return 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("GameDataProbe <sqpack path> logs|shops|audit|kornago-audit|positional-actions [loose|dump <job|from-to>]\nGameDataProbe <sqpack path> items|npc <name>\nGameDataProbe resources <plugin DLL path> [currency json path]");
    return 1;
}
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.Korean });
SpecialCurrencyResolver? probeCurrencyResolver = null;
Price DecodeCatalogCost(ExchangeCost cost)
{
    if (probeCurrencyResolver is null)
    {
        using var mapping = JsonDocument.Parse(File.ReadAllText("data/special-currencies.kr.json"));
        var tokens = mapping.RootElement.GetProperty("items").EnumerateObject().ToDictionary(p => uint.Parse(p.Name), p => p.Value.GetUInt32());
        var tomes = game.GetExcelSheet<TomestonesItem>()!.Where(t => t.Tomestones.RowId != 0 && t.Item.RowId != 0)
            .GroupBy(t => t.Tomestones.RowId).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Item.RowId);
        probeCurrencyResolver = new(tokens, tomes);
    }
    var id = probeCurrencyResolver.Resolve(cost.Type, cost.Id);
    var items = game.GetExcelSheet<Item>()!;
    var valid = cost.Quantity > 0 && id.HasValue && items.TryGetRow(id.Value, out var row) && !row.Name.IsEmpty;
    return new(new(id ?? cost.Id, cost.Type == 1 ? ItemQuality.High : ItemQuality.Normal, cost.Collectability),
        valid ? items.GetRow(id!.Value).Name.ExtractText() : "미확인", cost.Quantity, valid);
}
if (args[1] == "items" && args.Length >= 3)
{
    foreach (var item in game.GetExcelSheet<Item>()!.Where(i => uint.TryParse(args[2], out var id) ? i.RowId == id : i.Name.ExtractText().Contains(args[2], StringComparison.Ordinal)))
    {
        Console.WriteLine($"Item={item.RowId}; Name={item.Name.ExtractText()}; Category={item.ItemUICategory.RowId}/{item.ItemUICategory.ValueNullable?.Name.ExtractText()}; Search={item.ItemSearchCategory.RowId}/{item.ItemSearchCategory.ValueNullable?.Name.ExtractText()}; Action={item.ItemAction.RowId}/{item.ItemAction.ValueNullable?.Action.RowId}; ActionData={string.Join(',', item.ItemAction.ValueNullable?.Data.ToArray() ?? [])}; Additional={item.AdditionalData.RowType?.Name}/{item.AdditionalData.RowId}; Equip={item.EquipSlotCategory.RowId}");
        if (args.Length > 3 && args[3] == "description") Console.WriteLine(item.Description.ExtractText());
    }
}
else if (args[1] == "exchange-observed" && args.Length >= 4)
{
    var catalog = DuckbillTweak.Plugin.Shops.SheetExchangeCatalog.Load(game.GetExcelSheet<SpecialShop>()!);
    var page = args[3].Split(',').Select(id => new ExchangeObservation(uint.Parse(id), 1, [])).ToArray();
    var matched = catalog.Match(args[2], page, DecodeCatalogCost);
    Console.WriteLine($"ObservedPage={args[2]}; Rows={page.Length}; Matched={matched is not null}");
    if (matched is not null)
        for (var index = 0; index < page.Length; index++)
            Console.WriteLine($"Reward={page[index].RawItemId}; Quantity={matched[index].Quantity}; Prices={string.Join(',', matched[index].Prices.Select(price => $"{price.Key.ItemId}x{price.Quantity}/{price.Resolved}"))}");
}
else if (args[1] == "exchange-catalog-audit")
{
    var shops = game.GetExcelSheet<SpecialShop>()!;
    var catalog = DuckbillTweak.Plugin.Shops.SheetExchangeCatalog.Load(shops);
    var classifier = new DuckbillTweak.Plugin.Shops.ShopItemClassifier(shops, game.GetExcelSheet<Recipe>()!, game.GetExcelSheet<RacingChocoboItem>()!);
    var items = game.GetExcelSheet<Item>()!;
    var pages = 0; var recoveredPages = 0; var recoveredRows = 0; var missing = 0; var unknown = 0;
    foreach (var shop in shops)
    {
        var rows = shop.Item.Where(row => row.ReceiveItems.Any(reward => reward.Item.RowId != 0)).ToArray();
        if (rows.Length == 0 || rows.Any(row => row.ReceiveItems.Count(reward => reward.Item.RowId != 0) != 1)) continue;
        pages++;
        // Game rows can be reordered. Audit missing prices in a different order from the sheet.
        var displayed = rows.Reverse().ToArray();
        var page = displayed.Select(row => row.ReceiveItems.First(reward => reward.Item.RowId != 0))
            .Select(reward => new ExchangeObservation(reward.Item.RowId + (reward.ReceiveHq ? 1_000_000u : 0), 0, [])).ToArray();
        var matched = catalog.Match(shop.Name.ExtractText(), page, DecodeCatalogCost);
        if (matched is null) { missing++; if (missing <= 8) Console.WriteLine($"AmbiguousPage={shop.RowId}/{shop.Name.ExtractText()}"); continue; }
        recoveredPages++; recoveredRows += matched.Count;
        unknown += matched.Count(row => row.Prices.Any(price => !price.Resolved));
        if (!rows.Any(row => row.ItemCosts.Any(cost => args.Skip(2).Any(name => cost.ItemCost.IsValid && cost.ItemCost.Value.Name.ExtractText() == name)))) continue;
        var offers = displayed.Select((row, index) =>
        {
            var reward = row.ReceiveItems.First(reward => reward.Item.RowId != 0);
            var item = reward.Item.Value; var purpose = classifier.Classify(item);
            return new Offer($"{shop.RowId}:{index}", shop.Name.ExtractText(),
                [new(new(item.RowId, item.Name.ExtractText(), ShopCategory.GroupId(item.ItemUICategory.RowId, purpose),
                    ShopCategory.GroupName(item.ItemUICategory.Value.Name.ExtractText(), purpose), item.IsUntradable, purpose),
                    matched[index].Quantity, reward.ReceiveHq ? ItemQuality.High : ItemQuality.Normal)], matched[index].Prices, false);
        }).Where(offer => new ShopFilter { HideConsumables = true }.Matches(offer, _ => Ownership.Unknown)).ToArray();
        Console.WriteLine($"Target={shop.RowId}/{shop.Name.ExtractText()}; RecoveredRows={matched.Count}; CollectionOffers={offers.Length}");
        foreach (var total in PurchasePlanner.Calculate(offers, _ => null)) Console.WriteLine($" Required={total.Name}x{total.Required}; Owned=Unknown");
    }
    Console.WriteLine($"ReorderedSingleRewardPages={pages}; RecoveredPages={recoveredPages}; RecoveredRows={recoveredRows}; AmbiguousPages={missing}; UnresolvedCostRows={unknown}");
    foreach (var group in shops.SelectMany(shop => shop.Item.SelectMany(row => row.ItemCosts.Where(cost => cost.ItemCost.RowId != 0 && cost.CurrencyCost == 0)
                 .Select(cost => (Shop: shop, Cost: cost)))).GroupBy(entry => entry.Cost.CostType))
        Console.WriteLine($"ZeroCostType={group.Key}; Entries={group.Count()}; Examples={string.Join(';', group.Take(4).Select(entry => $"{entry.Shop.RowId}/{entry.Shop.Name.ExtractText()}/Item{entry.Cost.ItemCost.RowId}"))}");
}
else if (args[1] == "currency-offers" && args.Length >= 3)
{
    var names = args.Skip(2).ToArray();
    var targets = game.GetExcelSheet<Item>()!.Where(item => names.Any(name => item.Name.ExtractText() == name)).Select(item => item.RowId).ToHashSet();
    foreach (var shop in game.GetExcelSheet<SpecialShop>()!)
    {
        var offers = shop.Item.Where(offer => offer.ItemCosts.Any(cost => targets.Contains(cost.ItemCost.RowId))).ToArray();
        if (offers.Length == 0) continue;
        Console.WriteLine($"Shop={shop.RowId}/{shop.Name.ExtractText()}; Encoding={shop.UseCurrencyType}; Offers={offers.Length}");
        foreach (var offer in offers.Take(3))
            Console.WriteLine($" Receive={string.Join(',', offer.ReceiveItems.Where(reward => reward.Item.RowId != 0).Select(reward => $"{reward.Item.RowId}/{reward.Item.Value.Name.ExtractText()}x{reward.ReceiveCount}"))}; Cost={string.Join(',', offer.ItemCosts.Where(cost => cost.ItemCost.RowId != 0).Select(cost => $"{cost.ItemCost.RowId}x{cost.CurrencyCost}/type{cost.CostType}"))}");
    }
}
else if (args[1] == "category-order")
{
    foreach (var category in game.GetExcelSheet<ItemUICategory>()!.Where(c => !c.Name.IsEmpty)
                 .OrderBy(c => c.OrderMajor).ThenBy(c => c.OrderMinor).ThenBy(c => c.RowId))
        Console.WriteLine($"{category.OrderMajor:D2}/{category.OrderMinor:D2} {category.RowId}: {category.Name.ExtractText()}");
}
else if (args[1] == "category-groups")
{
    var classifier = new DuckbillTweak.Plugin.Shops.ShopItemClassifier(game.GetExcelSheet<SpecialShop>()!,
        game.GetExcelSheet<Recipe>()!, game.GetExcelSheet<RacingChocoboItem>()!);
    var items = game.GetExcelSheet<Item>()!.Where(item => !item.Name.IsEmpty).Select(item =>
    {
        var purpose = classifier.Classify(item);
        return (Item: item, Group: ShopCategory.GroupId(item.ItemUICategory.RowId, purpose), Purpose: purpose);
    }).ToArray();
    var equipment = items.Where(item => item.Group == ShopCategory.Equipment).ToArray();
    Console.WriteLine($"EquipmentGroup={ShopCategory.Equipment}; Items={equipment.Length}; NativeCategories={equipment.Select(item => item.Item.ItemUICategory.RowId).Distinct().Count()}; NonEquipment={equipment.Count(item => item.Purpose != ShopItemPurpose.Equipment)}; MissedEquipment={items.Count(item => item.Purpose == ShopItemPurpose.Equipment && item.Group != ShopCategory.Equipment)}");
    foreach (var category in equipment.GroupBy(item => item.Item.ItemUICategory.RowId))
        Console.WriteLine($"{category.Key}/{category.First().Item.ItemUICategory.Value.Name.ExtractText()} -> 장비 ({category.Count()})");
}
else if (args[1] == "policy-actions")
{
    foreach (var group in game.GetExcelSheet<Item>()!.Where(i => !i.Name.IsEmpty &&
        i.ItemAction.ValueNullable?.Action.RowId is 851 or 852 or 2483 or 2634 or 2645 or 3800 or 5136 or 9994 or 17290 or 31315 or 32865 or 34493 or 36120 or 47023)
        .GroupBy(i => i.ItemAction.Value.Action.RowId))
    {
        Console.WriteLine($"Action={group.Key}; Count={group.Count()}; Equipment={group.Count(i => i.EquipSlotCategory.RowId != 0)}");
        foreach (var item in group)
            Console.WriteLine($" {item.RowId}/{item.Name.ExtractText()}; {item.Description.ExtractText()}");
    }
    foreach (var group in game.GetExcelSheet<RacingChocoboItem>()!.Where(r => r.Item.RowId != 0).GroupBy(r => r.Category))
        Console.WriteLine($"RacingCategory={group.Key}; Count={group.Count()}; Items={string.Join(',', group.Select(r => $"{r.Item.RowId}/{r.Item.Value.Name.ExtractText()}"))}");
}
else if (args[1] == "shop-policy-gaps")
{
    var items = game.GetExcelSheet<Item>()!;
    var shops = game.GetExcelSheet<SpecialShop>()!;
    var classifier = new DuckbillTweak.Plugin.Shops.ShopItemClassifier(shops, game.GetExcelSheet<Recipe>()!, game.GetExcelSheet<RacingChocoboItem>()!);
    var sold = shops.SelectMany(s => s.Item).SelectMany(o => o.ReceiveItems).Where(r => r.Item.RowId != 0).Select(r => r.Item.RowId)
        .Concat(game.GetSubrowExcelSheet<GilShopItem>()!.SelectMany(r => r).Select(r => r.Item.RowId)).ToHashSet();
    var candidates = items.Where(i => !i.Name.IsEmpty && sold.Contains(i.RowId) && i.EquipSlotCategory.RowId == 0 &&
        !ShopItemPolicy.Excluded(classifier.Classify(i))).ToArray();
    foreach (var group in candidates.GroupBy(i => (classifier.Classify(i), i.ItemAction.ValueNullable?.Action.RowId ?? 0, i.AdditionalData.RowType?.Name)))
    {
        Console.WriteLine($"Group={group.Key}; Count={group.Count()}");
        foreach (var item in group.Take(group.Key.Item1 == ShopItemPurpose.Other ? 1000 : 8))
            Console.WriteLine($"  {item.RowId}/{item.Name.ExtractText()}; Category={item.ItemUICategory.RowId}; Search={item.ItemSearchCategory.RowId}; Action={item.ItemAction.RowId}; Additional={item.AdditionalData.RowId}");
    }
}
else if (args[1] == "shop-policy-audit")
{
    var items = game.GetExcelSheet<Item>()!;
    var shops = game.GetExcelSheet<SpecialShop>()!;
    var classifier = new DuckbillTweak.Plugin.Shops.ShopItemClassifier(shops, game.GetExcelSheet<Recipe>()!, game.GetExcelSheet<RacingChocoboItem>()!);
    var classified = items.Where(i => !i.Name.IsEmpty).Select(i => (Item: i, Purpose: classifier.Classify(i))).ToArray();
    foreach (var group in classified.GroupBy(x => x.Purpose))
    {
        var item = group.First().Item;
        var offer = new Offer($"audit:{item.RowId}", "", [new(new(item.RowId, item.Name.ExtractText(), item.ItemUICategory.RowId, "", item.IsUntradable, group.Key), 1, ItemQuality.Normal)], [], true);
        var ownedFilter = new ShopFilter { HideOwned = true };
        var collectionFilter = new ShopFilter { HideOwned = false, HideConsumables = true };
        Console.WriteLine($"Purpose={group.Key}; Count={group.Count()}; Excluded={ShopItemPolicy.Excluded(group.Key)}; Equipped={group.Count(x => x.Item.EquipSlotCategory.RowId != 0)}; OwnedFilterKeepsOwned={ownedFilter.Matches(offer, _ => Ownership.Owned)}; OwnedFilterKeepsUnknown={ownedFilter.Matches(offer, _ => Ownership.Unknown)}; CollectionFilterKeeps={collectionFilter.Matches(offer, _ => Ownership.Owned)}");
    }
    var excludedUnlocks = classified.Where(x => ShopItemPolicy.Excluded(x.Purpose) &&
        ShopItemPolicy.IsCollectionAction(x.Item.ItemAction.ValueNullable?.Action.RowId ?? 0)).ToArray();
    Console.WriteLine($"ExcludedPermanentUnlocks={excludedUnlocks.Length}");
    foreach (var entry in excludedUnlocks) Console.WriteLine($"Review={entry.Item.RowId}/{entry.Item.Name.ExtractText()}/{entry.Purpose}");
    foreach (var entry in classified.Where(x => x.Item.Name.ExtractText().Contains("강화약", StringComparison.Ordinal) ||
                 x.Item.RowId is 24337 or 14273 or 14274 or 14952 or 6954 or 6955 or 50847 or 4868 or 5890 or 7153 or 8837 or
                     33328 or 9615 or 9852 or 17723 or 16784 or 49121 or 27593 or 50414 or 10128 or 30137 or 49806 or 46825 or 49787 ||
                 args.Skip(2).Any(value => uint.TryParse(value, out var id) && id == x.Item.RowId)))
        Console.WriteLine($"Item={entry.Item.RowId}/{entry.Item.Name.ExtractText()}; Purpose={entry.Purpose}; Excluded={ShopItemPolicy.Excluded(entry.Purpose)}; HideWhenOwned={ShopItemPolicy.CanHideOwned(entry.Purpose)}");
    // Use the production filter/planner on the real 11-row coin exchange. No ownership is assumed.
    foreach (var shop in shops.Where(s => s.Item.Any(o => o.ItemCosts.Any(c => c.ItemCost.RowId == 50847))))
    {
        var offers = shop.Item.Where(o => o.ReceiveItems.Any(r => r.Item.RowId != 0)).Select((o, index) => new Offer(
            $"{shop.RowId}:{index}", shop.Name.ExtractText(), o.ReceiveItems.Where(r => r.Item.RowId != 0).Select(r =>
                new Reward(new ShopItem(r.Item.RowId, r.Item.Value.Name.ExtractText(), r.Item.Value.ItemUICategory.RowId, "", r.Item.Value.IsUntradable,
                    classifier.Classify(r.Item.Value)), r.ReceiveCount, r.ReceiveHq ? ItemQuality.High : ItemQuality.Normal)).ToArray(),
            o.ItemCosts.Where(c => c.ItemCost.RowId != 0).Select(c => new Price(new(c.ItemCost.RowId, c.CostType == 1 ? ItemQuality.High : ItemQuality.Normal, c.CollectabilityCost),
                items.GetRow(c.ItemCost.RowId).Name.ExtractText(), c.CurrencyCost, c.CostType is 0 or 1)).ToArray(), false)).ToArray();
        var filter = new ShopFilter { HideConsumables = true };
        var selected = offers.Where(o => filter.Matches(o, _ => Ownership.Unknown)).ToArray();
        var defaults = PurchasePlanner.SelectDefaults(selected);
        var totals = PurchasePlanner.Calculate(selected.Where(o => defaults.Contains(o.Id)), _ => null);
        Console.WriteLine($"CoinShop={shop.RowId}; Rows={offers.Length}; FilteredRows={selected.Length}; EligibilityUnknownIncluded={selected.Count(o => !o.ConditionsKnown)}");
        foreach (var offer in offers) Console.WriteLine($"  {offer.Rewards[0].Item.Name}; Purpose={offer.Rewards[0].Item.Purpose}; Included={defaults.Contains(offer.Id)}; Receive={offer.Rewards[0].Quantity}; Cost={string.Join(',', offer.Prices.Select(p => $"{p.Name}x{p.Quantity}"))}");
        foreach (var total in totals) Console.WriteLine($"  Required={total.Name}x{total.Required}; Owned={total.Owned?.ToString() ?? "Unknown"}");
    }
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
else if (args[1] == "positional-actions")
{
    // Offline candidate list for docs/PLANS.md: player actions whose KR tooltip mentions a flank/rear condition.
    var transients = game.GetExcelSheet<ActionTransient>()!;
    var count = 0;
    foreach (var action in game.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.Where(a => !a.IsPvP && (a.ClassJobLevel > 0 || args is [_, _, "dump", _])))
    {
        if (!transients.TryGetRow(action.RowId, out var transient)) continue;
        if (args.Length > 3 && args[2] == "dump" && !(action.ClassJob.ValueNullable?.Abbreviation.ExtractText() == args[3]
            || args[3].Split('-') is [var from, var to] && uint.TryParse(from, out var low) && uint.TryParse(to, out var high) && action.RowId >= low && action.RowId <= high)) continue;
        // Conditional tooltip lines live inside macros that ExtractText drops, so match the macro form as well.
        var raw = transient.Description.ToString();
        if (args.Length > 3 && args[2] == "dump")
        {
            Console.WriteLine($"Dump={action.RowId}; Name={action.Name.ExtractText()}; Level={action.ClassJobLevel}; Raw={raw.Replace('\n', ' ')}; Macro={transient.Description.ToMacroString().Replace('\n', ' ')}");
            continue;
        }
        var flank = raw.Contains("측면공격") || raw.Contains("측면 공격");
        var rear = raw.Contains("후방공격") || raw.Contains("후방 공격");
        if (args.Length > 2 && args[2] == "loose" && !flank && !rear && (raw.Contains("측면") || raw.Contains("후방")))
        {
            var marker = raw.IndexOf(raw.Contains("측면") ? "측면" : "후방", StringComparison.Ordinal);
            Console.WriteLine($"Loose={action.RowId}; Name={action.Name.ExtractText()}; Job={action.ClassJob.ValueNullable?.Abbreviation.ExtractText()}; Raw={raw.Substring(Math.Max(0, marker - 40), Math.Min(100, raw.Length - Math.Max(0, marker - 40))).Replace('\n', ' ')}");
            continue;
        }
        if (!flank && !rear) continue;
        Console.WriteLine($"Action={action.RowId}; Name={action.Name.ExtractText()}; Job={action.ClassJobCategory.ValueNullable?.Name.ExtractText()}; Level={action.ClassJobLevel}; Flank={flank}; Rear={rear}");
        count++;
    }
    Console.WriteLine($"PositionalCandidates={count}");
}
else if (args[1] == "gfd-icons" && args.Length >= 4)
{
    // Renders inline game icons (BitmapFontIcon ids) from common/font/gfdata.gfd + fonticon texture into one PNG sheet,
    // one 4x-scaled cell per id in the given order, so icon choices can be reviewed by eye. Offline, read-only.
    var gfd = game.GetFile("common/font/gfdata.gfd") ?? throw new InvalidDataException("gfdata.gfd not found.");
    var data = gfd.Data;
    var count = BitConverter.ToInt32(data, 8);
    var entries = new Dictionary<int, (int Left, int Top, int Width, int Height)>();
    for (var i = 0; i < count; i++)
    {
        var offset = 0x10 + i * 0x10;
        if (offset + 0x10 > data.Length) break;
        entries[BitConverter.ToUInt16(data, offset)] = (BitConverter.ToUInt16(data, offset + 2), BitConverter.ToUInt16(data, offset + 4),
            BitConverter.ToUInt16(data, offset + 6), BitConverter.ToUInt16(data, offset + 8));
    }
    var texture = game.GetFile<TexFile>("common/font/fonticon_xinput.tex") ?? throw new InvalidDataException("fonticon texture not found.");
    var pixels = texture.ImageData; // BGRA8
    var ids = args[3].Split(',').Select(int.Parse).ToArray();
    const int scale = 4, cell = 30 * scale;
    var sheetWidth = cell * Math.Min(ids.Length, 6);
    var sheetHeight = cell * ((ids.Length + 5) / 6);
    var sheet = new byte[sheetWidth * sheetHeight * 4];
    for (var p = 0; p < sheet.Length; p += 4) { sheet[p] = 60; sheet[p + 1] = 60; sheet[p + 2] = 60; sheet[p + 3] = 255; } // grey background
    for (var n = 0; n < ids.Length; n++)
    {
        if (!entries.TryGetValue(ids[n], out var e)) { Console.WriteLine($"Icon={ids[n]}; Missing"); continue; }
        Console.WriteLine($"Cell={n}; Icon={ids[n]}; Rect={e.Left},{e.Top},{e.Width}x{e.Height}");
        var originX = (n % 6) * cell + 4; var originY = (n / 6) * cell + 4;
        for (var y = 0; y < e.Height * scale; y++)
        for (var x = 0; x < e.Width * scale; x++)
        {
            var sx = e.Left + x / scale; var sy = e.Top + y / scale;
            if (sx >= texture.Header.Width || sy >= texture.Header.Height) continue;
            var src = (sy * texture.Header.Width + sx) * 4;
            var dx = originX + x; var dy = originY + y;
            if (dx >= sheetWidth || dy >= sheetHeight) continue;
            var dst = (dy * sheetWidth + dx) * 4;
            var alpha = pixels[src + 3] / 255.0;
            for (var c = 0; c < 3; c++) sheet[dst + c] = (byte)(pixels[src + 2 - c] * alpha + sheet[dst + c] * (1 - alpha)); // BGRA -> RGB blend
        }
    }
    File.WriteAllBytes(args[2], EncodePng(sheet, sheetWidth, sheetHeight));
    Console.WriteLine($"Sheet={args[2]}; Size={sheetWidth}x{sheetHeight}; Texture={texture.Header.Width}x{texture.Header.Height}");
}
else if (args[1] == "emotes")
{
    // Offline research for emote sync: each emote's ActionTimeline slots (key, slot, loop flag) and emote mode.
    var filter = args.Length > 2 ? args[2] : null;
    foreach (var emote in game.GetExcelSheet<Emote>()!.Where(e => !e.Name.IsEmpty))
    {
        var name = emote.Name.ExtractText();
        if (filter is not null && !name.Contains(filter, StringComparison.Ordinal) && emote.RowId.ToString() != filter) continue;
        var timelines = emote.ActionTimeline.Select((timeline, index) => (timeline, index)).Where(t => t.timeline.RowId != 0)
            .Select(t => $"{t.index}:{t.timeline.RowId}/{t.timeline.ValueNullable?.Key.ExtractText()}/slot{t.timeline.ValueNullable?.Slot}/loop{(t.timeline.ValueNullable?.IsLoop == true ? 1 : 0)}");
        Console.WriteLine($"Emote={emote.RowId}; Name={name}; Category={emote.EmoteCategory.RowId}; Mode={emote.EmoteMode.RowId}; Command={emote.TextCommand.ValueNullable?.Command.ExtractText()}; Timelines=[{string.Join(" ", timelines)}]");
    }
}
else if (args[1] == "combo-actions")
{
    // Offline candidate list: non-PvP job actions whose KR tooltip macro has a combo potency line.
    var transients = game.GetExcelSheet<ActionTransient>()!;
    var ids = new List<uint>();
    foreach (var action in game.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.Where(a => !a.IsPvP && a.ClassJobLevel > 0))
    {
        if (!transients.TryGetRow(action.RowId, out var transient)) continue;
        if (!transient.Description.ToMacroString().Contains("콤보 시 위력")) continue;
        ids.Add(action.RowId);
        Console.WriteLine($"Combo={action.RowId}; Name={action.Name.ExtractText()}; Category={action.ClassJobCategory.ValueNullable?.Name.ExtractText()}; Level={action.ClassJobLevel}");
    }
    Console.WriteLine($"ComboCandidates={ids.Count}; Ids={string.Join(", ", ids)}");
}
else if (args[1] == "bonus-coverage")
{
    // Runs the shipped Core tooltip builder for every candidate, job and level, i.e. exactly what the plugin evaluates
    // in game. Prints contiguous level ranges with the derived potencies and state bytes, or the rejection reason.
    var actions = game.GetExcelSheet<Lumina.Excel.Sheets.Action>()!;
    var transients = game.GetExcelSheet<ActionTransient>()!;
    var classJobs = game.GetExcelSheet<ClassJob>()!.Where(job => job.RowId != 0 && !job.Name.IsEmpty)
        .Select(job => (Id: (int)job.RowId, Name: job.Name.ExtractText())).ToList();
    int supported = 0, rejected = 0;
    var rejectedActions = new SortedSet<uint>();
    foreach (var id in DuckbillTweak.Core.Combat.BonusTooltip.Candidates)
    {
        var action = actions.GetRow(id);
        var macro = transients.GetRow(id).Description.ToMacroString();
        var category = action.ClassJobCategory.ValueNullable?.Name.ExtractText() ?? "";
        var words = category.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var jobs = classJobs.Where(job => words.Contains(job.Name)).Select(job => job.Id).ToList();
        Console.WriteLine($"Action={id}; Name={action.Name.ExtractText()}; Category={category}; Learned={action.ClassJobLevel}; Jobs={string.Join(',', jobs)}");
        if (jobs.Count == 0) { Console.WriteLine("  NO JOB RESOLVED"); rejectedActions.Add(id); }
        foreach (var job in jobs)
        {
            string? previous = null; var from = 0;
            for (var level = Math.Max(1, (int)action.ClassJobLevel); level <= 101; level++)
            {
                var summary = "";
                if (level <= 100)
                {
                    var built = DuckbillTweak.Core.Combat.BonusTooltip.Build(id, macro, job, level, out var reason);
                    if (built is null) { rejected++; rejectedActions.Add(id); summary = $"REJECT {reason}"; }
                    else
                    {
                        supported++;
                        var context = new DuckbillTweak.Core.Combat.BonusContext(new HashSet<uint>(), 0, 0);
                        var raised = new DuckbillTweak.Core.Combat.BonusContext(new HashSet<uint> { built.ModifierStatus }, 1, 1);
                        summary = $"{built.Side} Base={built.Base} Combo={built.Combo} Pos=+{built.Positional} From={built.ComboFrom ?? "-"}" +
                            $"{(built.ComboOnly ? " ComboOnly" : "")}" +
                            $"{(built.Modifier == DuckbillTweak.Core.Combat.PositionalModifier.None ? "" : $" Mod={built.Modifier}:{built.ModifierStatus}+{built.ModifierPotency} Raised[{DuckbillTweak.Core.Combat.BonusJudge.Describe(built, raised)}]")}" +
                            $" Bytes[{DuckbillTweak.Core.Combat.BonusJudge.Describe(built, context)}]";
                    }
                }
                if (summary != previous)
                {
                    if (previous is not null) Console.WriteLine($"  Job={job}; Levels={from}-{level - 1}; {previous}");
                    previous = summary; from = level;
                }
            }
        }
    }
    Console.WriteLine($"BonusCoverage: SupportedJobLevels={supported}; RejectedJobLevels={rejected}; ActionsWithRejections={string.Join(',', rejectedActions)}");
}
else if (args[1] == "bonus-replay" && args.Length >= 3)
{
    // Replays "/dbtw probe" effect lines through the same Core builder + decoder the plugin uses, at the logged job/level.
    // Positional truth = client zone label (or True North). Combo truth = previous action name matches the tooltip's combo
    // condition at that level (or Meikyo Shisui for Samurai), so only clean test hits are meaningful.
    var actions = game.GetExcelSheet<Lumina.Excel.Sheets.Action>()!;
    var transients = game.GetExcelSheet<ActionTransient>()!;
    int ok = 0, wrong = 0, notJudged = 0, rejectedRows = 0;
    var pattern = new System.Text.RegularExpressions.Regex(@"effect t=\d+ action=(\d+)/.*? job=(\d+) level=(\d+) comboAtReceive=(\d+)/([\d.]+) trueNorth=(\w+) statuses=\[(.*?)\](?: monkFury=\S*coeurl(\d+))?(?: kazematoi=(\d+))? target=.*? relative=(-?\d+)deg zone=(\w+).*type=3 p=\[.. .. (..)");
    foreach (var line in File.ReadLines(args[2]))
    {
        var m = pattern.Match(line);
        if (!m.Success) continue;
        var id = uint.Parse(m.Groups[1].Value);
        if (!DuckbillTweak.Core.Combat.BonusTooltip.Candidates.Contains(id)) continue;
        var job = int.Parse(m.Groups[2].Value); var level = int.Parse(m.Groups[3].Value);
        var action = DuckbillTweak.Core.Combat.BonusTooltip.Build(id, transients.GetRow(id).Description.ToMacroString(), job, level, out var reason);
        if (action is null) { rejectedRows++; Console.WriteLine($"Rejected: {line[11..23]} Action={id} Job={job} Level={level} {reason}"); continue; }
        var statuses = m.Groups[7].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => uint.Parse(s.Split('x')[0])).ToHashSet();
        var context = new DuckbillTweak.Core.Combat.BonusContext(statuses,
            m.Groups[8].Success ? int.Parse(m.Groups[8].Value) : 0, m.Groups[9].Success ? int.Parse(m.Groups[9].Value) : 0);
        var bonus = Convert.ToInt32(m.Groups[12].Value, 16);
        var zone = m.Groups[11].Value;
        var comboId = uint.Parse(m.Groups[4].Value);
        var comboName = comboId == 0 ? null : actions.GetRow(comboId).Name.ExtractText();
        bool? comboTruth = action.CanMissCombo
            // Tooltips may list alternatives: "A 또는 B".
            ? (double.Parse(m.Groups[5].Value) > 0 && comboName is not null && action.ComboFrom is not null
                && action.ComboFrom.Split(" 또는 ").Contains(comboName)) || (job == 34 && statuses.Contains(1233))
            : null;
        bool? positionalTruth = action.HasPositional
            ? m.Groups[6].Value == "True" || (action.Side == DuckbillTweak.Core.Combat.PositionalSide.Flank ? zone == "Flank" : zone == "Rear")
            : null;
        var result = DuckbillTweak.Core.Combat.BonusJudge.Decode(action, context, bonus);
        if (result is null)
        {
            notJudged++;
            Console.WriteLine($"NotJudged: {line[11..23]} Action={id} Zone={zone} Bonus={bonus} States=[{DuckbillTweak.Core.Combat.BonusJudge.Describe(action, context)}] Kazematoi={(m.Groups[9].Success ? m.Groups[9].Value : "not-logged")}");
        }
        else if (result.Value.ComboApplied == comboTruth && result.Value.PositionalHit == positionalTruth) ok++;
        else
        {
            wrong++;
            Console.WriteLine($"WRONG: {line[11..23]} Action={id} Zone={zone}({m.Groups[10].Value}) Combo={comboName ?? "-"} Bonus={bonus} Decoded={result} Truth=({comboTruth},{positionalTruth})");
        }
    }
    Console.WriteLine($"BonusReplay: Correct={ok}; Wrong={wrong}; NotJudged={notJudged}; RejectedRows={rejectedRows}");
    return wrong == 0 ? 0 : 2;
}
else if (args[1] == "territory-uses")
{
    // Offline map of TerritoryIntendedUse ids to sample place names, used to exclude special fields from positional judging.
    foreach (var group in game.GetExcelSheet<TerritoryType>()!.Where(t => t.PlaceName.RowId != 0)
        .GroupBy(t => t.TerritoryIntendedUse.RowId).OrderBy(g => g.Key))
        Console.WriteLine($"Use={group.Key}; Count={group.Count()}; Samples={string.Join(", ", group.Select(t => t.PlaceName.Value.Name.ExtractText()).Where(n => n.Length > 0).Distinct().Take(4))}");
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
    var activeOffers = game.GetExcelSheet<SpecialShop>()!.SelectMany(s => s.Item)
        .Where(o => o.ReceiveItems.Any(r => r.Item.RowId != 0)).ToArray();
    Console.WriteLine($"Offers={activeOffers.Length}; MultipleCosts={activeOffers.Count(o => o.ItemCosts.Count(c => c.ItemCost.RowId != 0) > 1)}; MultipleRewards={activeOffers.Count(o => o.ReceiveItems.Count(r => r.Item.RowId != 0) > 1)}; BundledQuantity={activeOffers.Count(o => o.ReceiveItems.Any(r => r.Item.RowId != 0 && r.ReceiveCount > 1))}");
    foreach (var group in activeOffers.SelectMany(o => o.ItemCosts).Where(c => c.ItemCost.RowId != 0).GroupBy(c => c.CostType))
        Console.WriteLine($"CostType={group.Key}; Prices={group.Count()}; DistinctReferences={group.Select(c => c.ItemCost.RowId).Distinct().Count()}; Collectability={group.Count(c => c.CollectabilityCost != 0)}");
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

// Minimal RGBA PNG encoder (no external packages) for offline review images.
static byte[] EncodePng(byte[] rgba, int width, int height)
{
    var raw = new MemoryStream();
    for (var y = 0; y < height; y++)
    {
        raw.WriteByte(0);
        raw.Write(rgba, y * width * 4, width * 4);
    }
    var compressed = new MemoryStream();
    using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        raw.WriteTo(zlib);
    var output = new MemoryStream();
    output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    void Chunk(string type, byte[] body)
    {
        var length = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, body.Length);
        output.Write(length);
        var typed = System.Text.Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
        output.Write(typed);
        uint crc = 0xFFFFFFFF;
        foreach (var b in typed)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        var crcBytes = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        output.Write(crcBytes);
    }
    var header = new byte[13];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
    header[8] = 8; header[9] = 6; // 8-bit RGBA
    Chunk("IHDR", header);
    Chunk("IDAT", compressed.ToArray());
    Chunk("IEND", []);
    return output.ToArray();
}

/// <summary>Offline INativeCodeReader over an executable file: matches .text, addresses are preferred-base VAs.</summary>
sealed class FileCodeReader : INativeCodeReader
{
    private readonly byte[] bytes;
    private readonly PEReader pe;
    private readonly ulong imageBase;

    public FileCodeReader(byte[] bytes)
    {
        this.bytes = bytes;
        pe = new PEReader(new MemoryStream(bytes));
        imageBase = pe.PEHeaders.PEHeader!.ImageBase;
    }

    public long Rva(nint address) => (long)((ulong)address - imageBase);

    public IReadOnlyList<nint> ScanAll(string pattern)
    {
        var parsed = NativeCode.Parse(pattern);
        var text = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".text");
        var span = bytes.AsSpan(text.PointerToRawData, Math.Min(text.SizeOfRawData, text.VirtualSize));
        var matches = new List<nint>();
        for (var offset = 0; offset <= span.Length - parsed.Length; offset++)
            if (NativeCode.Match(span[offset..], parsed)) matches.Add((nint)(imageBase + (ulong)(text.VirtualAddress + offset)));
        return matches;
    }

    public bool TryRead(nint address, int length, out byte[] result)
    {
        result = [];
        var rva = Rva(address);
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (rva < section.VirtualAddress || rva + length > section.VirtualAddress + section.SizeOfRawData) continue;
            result = bytes.AsSpan((int)(section.PointerToRawData + rva - section.VirtualAddress), length).ToArray();
            return true;
        }
        return false;
    }
}
