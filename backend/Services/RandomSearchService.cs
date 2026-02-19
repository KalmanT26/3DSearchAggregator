using ModelAggregator.Api.DTOs;

namespace ModelAggregator.Api.Services;

/// <summary>
/// Provides random, relevant 3D printing search terms using a weighted, categorized static list
/// combined with a dynamic mutation pipeline (adjective injection, compound assembly, seasonal awareness).
/// 
/// Mutation pipeline (applied per request):
///   40% → plain base term
///   30% → adjective/modifier + base term
///   20% → compound assembly (character/theme + object)
///   10% → seasonal term (month-aware)
/// </summary>
public class RandomSearchService : IRandomSearchService
{
    // ── Base Terms ────────────────────────────────────────────────────────────

    private record Category(string Name, int Weight, string[] Terms);

    private static readonly Category[] _categories =
    [
        new("Everyday Functional", 30,
        [
            "phone stand", "cable organizer", "headphone holder", "wall hook", "key holder",
            "coaster", "bookend", "bag clip", "door stop", "cable clip", "soap dish",
            "toothbrush holder", "toilet paper holder", "towel hook", "shelf bracket",
            "drawer organizer", "desk organizer", "pen holder", "pencil cup", "monitor stand",
            "laptop stand", "tablet stand", "charging station", "cord management", "cup holder",
            "bottle opener", "spice rack", "napkin holder", "planter", "plant pot",
            "succulent planter", "hanging planter", "flower pot", "seed tray"
        ]),

        new("Storage & Organization", 20,
        [
            "gridfinity", "storage box", "storage bin", "organizer tray", "parts bin",
            "tool holder", "screwdriver holder", "wrench holder", "bit holder", "drill bit organizer",
            "sd card holder", "battery holder", "cable box", "remote holder", "game cartridge holder",
            "lego storage", "filament spool holder", "tool wall mount", "pegboard hook", "workshop organizer"
        ]),

        new("Art & Sculpture", 15,
        [
            "low poly", "voronoi", "lithophane", "bust", "sculpture", "vase", "spiral vase",
            "geometric art", "abstract sculpture", "wireframe", "faceted", "parametric art",
            "mandala", "fractal", "impossible object", "optical illusion", "infinity cube",
            "wave pattern", "lattice", "gyroid"
        ]),

        new("Toys & Games", 15,
        [
            "articulated dragon", "flexi rex", "flexi animal", "fidget toy", "fidget spinner",
            "puzzle", "interlocking puzzle", "brain teaser", "chess set", "chess piece",
            "dice", "dice tower", "board game insert", "card holder", "token", "game piece",
            "marble run", "spinning top", "yo-yo", "kaleidoscope"
        ]),

        new("Tech & Electronics", 10,
        [
            "raspberry pi case", "raspberry pi 5 case", "arduino enclosure", "esp32 case",
            "electronics enclosure", "pcb mount", "fan duct", "cable management", "server rack",
            "network switch mount", "camera mount", "gopro mount", "webcam mount", "ring light mount",
            "microphone stand", "keyboard case", "mouse shell", "pc case mod", "nvme enclosure"
        ]),

        new("Hobby & Maker", 5,
        [
            "cosplay", "helmet", "mask", "armor", "prop", "wand", "lightsaber",
            "dnd miniature", "rpg terrain", "dungeon tile", "warhammer terrain", "miniature base",
            "tabletop scenery", "model train", "rc car part", "drone frame", "quadcopter"
        ]),

        new("3D Printing Tools", 5,
        [
            "print in place", "support free", "voron part", "prusa upgrade", "ender 3 upgrade",
            "bed leveling tool", "filament guide", "nozzle cleaner", "calibration cube",
            "test print", "benchy", "xyz cube", "overhang test", "stringing test"
        ]),
    ];

    // ── Mutation Data ─────────────────────────────────────────────────────────

    /// <summary>Style/aesthetic modifiers that work well as prefixes for most base terms.</summary>
    private static readonly string[] _styleModifiers =
    [
        "minimalist", "geometric", "art deco", "steampunk", "sci-fi", "gothic", "organic",
        "modular", "parametric", "cyberpunk", "retro", "futuristic", "industrial", "nordic",
        "japanese", "brutalist", "bauhaus", "victorian", "biomechanical", "abstract"
    ];

    /// <summary>Physical/functional modifiers that add specificity.</summary>
    private static readonly string[] _functionalModifiers =
    [
        "articulated", "foldable", "stackable", "magnetic", "snap-fit", "wall-mounted",
        "hanging", "desktop", "compact", "modular", "adjustable", "collapsible",
        "interlocking", "hollow", "lattice", "textured", "ribbed", "perforated"
    ];

    /// <summary>Character/theme nouns for compound assembly (left side).</summary>
    private static readonly string[] _compoundThemes =
    [
        "dragon", "skull", "octopus", "cat", "wolf", "fox", "bear", "owl", "raven", "phoenix",
        "robot", "alien", "astronaut", "knight", "samurai", "viking", "wizard", "pirate",
        "mushroom", "cactus", "crystal", "gear", "anchor", "compass", "moon", "saturn"
    ];

    /// <summary>Object nouns for compound assembly (right side — things that make sense with a theme).</summary>
    private static readonly string[] _compoundObjects =
    [
        "planter", "bookend", "pen holder", "lamp", "coaster", "wall art", "keychain",
        "figurine", "bust", "vase", "candle holder", "phone stand", "cable holder",
        "storage box", "dice tower", "miniature", "wall hook", "night light"
    ];

    // ── Seasonal Terms ────────────────────────────────────────────────────────

    private static readonly (int[] Months, string[] Terms)[] _seasonal =
    [
        ([ 1, 2 ],  [ "valentine heart", "love token", "cupid arrow", "winter decoration", "snowflake ornament" ]),
        ([ 3, 4 ],  [ "easter egg", "spring planter", "bunny figurine", "flower decoration", "butterfly" ]),
        ([ 5, 6 ],  [ "graduation cap", "father's day gift", "summer decoration", "beach themed", "sunflower" ]),
        ([ 7, 8 ],  [ "summer vase", "beach coaster", "tropical planter", "bbq tool holder", "camping gear" ]),
        ([ 9, 10 ], [ "halloween skull", "pumpkin", "ghost decoration", "bat ornament", "spider web", "witch hat", "jack o lantern" ]),
        ([ 11 ],    [ "thanksgiving decoration", "autumn leaf", "cornucopia", "harvest decoration" ]),
        ([ 12 ],    [ "christmas ornament", "snowflake", "advent calendar", "santa", "christmas tree", "star ornament", "gift box" ]),
    ];

    // ── Weighted Pool ─────────────────────────────────────────────────────────

    private static readonly string[] _weightedPool = BuildWeightedPool();

    private static string[] BuildWeightedPool()
    {
        var pool = new List<string>();
        foreach (var cat in _categories)
            for (int i = 0; i < cat.Weight; i++)
                pool.AddRange(cat.Terms);
        return [.. pool];
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public Task<string> GetRandomTermAsync(CancellationToken ct = default)
    {
        var roll = Random.Shared.NextDouble();

        string term = roll switch
        {
            < 0.10 => GetSeasonalTerm(),           // 10% seasonal
            < 0.30 => GetCompoundTerm(),            // 20% compound assembly
            < 0.60 => GetModifiedTerm(),            // 30% adjective + base
            _      => GetBaseTerm()                 // 40% plain base
        };

        return Task.FromResult(term);
    }

    public void IngestTrendingTags(IEnumerable<ModelDto> models) { }
    public Task RefreshDynamicTagsAsync(CancellationToken ct = default) => Task.CompletedTask;

    // ── Mutation Methods ──────────────────────────────────────────────────────

    private static string GetBaseTerm()
        => _weightedPool[Random.Shared.Next(_weightedPool.Length)];

    private static string GetModifiedTerm()
    {
        var baseTerm = GetBaseTerm();
        // 50/50 between style modifier and functional modifier
        var modifiers = Random.Shared.NextDouble() < 0.5 ? _styleModifiers : _functionalModifiers;
        var modifier = modifiers[Random.Shared.Next(modifiers.Length)];
        return $"{modifier} {baseTerm}";
    }

    private static string GetCompoundTerm()
    {
        var theme = _compoundThemes[Random.Shared.Next(_compoundThemes.Length)];
        var obj   = _compoundObjects[Random.Shared.Next(_compoundObjects.Length)];
        return $"{theme} {obj}";
    }

    private static string GetSeasonalTerm()
    {
        var month = DateTime.Now.Month;
        foreach (var (months, terms) in _seasonal)
        {
            if (Array.IndexOf(months, month) >= 0)
                return terms[Random.Shared.Next(terms.Length)];
        }
        // No seasonal match (shouldn't happen) — fall back to base
        return GetBaseTerm();
    }
}
