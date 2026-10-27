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
    foreach (var item in game.GetExcelSheet<Item>()!.Where(i => i.Name.ExtractText().Contains(args[2], StringComparison.Ordinal)))
        Console.WriteLine($"Item={item.RowId}; Name={item.Name.ExtractText()}; Category={item.ItemUICategory.RowId}/{item.ItemUICategory.ValueNullable?.Name.ExtractText()}; Search={item.ItemSearchCategory.RowId}; Action={item.ItemAction.RowId}/{item.ItemAction.ValueNullable?.Action.RowId}; ActionData={string.Join(',', item.ItemAction.ValueNullable?.Data.ToArray() ?? [])}; Additional={item.AdditionalData.RowType?.Name}/{item.AdditionalData.RowId}; Equip={item.EquipSlotCategory.RowId}");
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
                     33328 or 9615 or 9852 or 17723 or 16784 or 49121 or 27593 or 50414 or 10128 or 30137 or 49806))
        Console.WriteLine($"Item={entry.Item.RowId}/{entry.Item.Name.ExtractText()}; Purpose={entry.Purpose}; Excluded={ShopItemPolicy.Excluded(entry.Purpose)}");
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
