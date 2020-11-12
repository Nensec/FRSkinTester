﻿using System.ComponentModel;

namespace FRTools.Data
{
    public enum DragonType
    {
        Fae = 1,
        Guardian,
        Mirror,
        Pearlcatcher,
        Ridgeback,
        Tundra,
        Spiral,
        Imperial,
        Snapper,
        Wildclaw,
        Nocturne,
        Coatl,
        Skydancer,
        Bogsneak,
        Gaoler = 17,
        Banescale,
        Veilspun
    }

    // $('select[name="tertgene"] option[style*="display: block"]').map(function() { return this.outerHTML; })

    public enum GaolerBodyGene
    {
        Basic = 0,
        Giraffe = 27,
        Wasp,
        Shaggy,
        Falcon,
        Piebald,
        Pinstripe,
        Jaguar,
        Bar,
        Tapir,
        Tiger,
        Crystal,
        Mosaic,
        Phantom,
    }

    public enum GaolerWingGene
    {
        Basic = 0,
        Hex = 27,
        Bee,
        Streak,
        Peregrine,
        Paint,
        Trail,
        Rosette,
        Daub,
        Striation,
        Stripes,
        Facet,
        Breakup,
        Spirit,
    }

    public enum GaolerTertGene
    {
        Basic = 0,
        Fans = 3,
        Veined = 2,
        Ghost = 25,
        Shardflank,
        Gnarlhorns,
        Smoke,
        Thylacine,
        Ringlets,
        Underbelly,
        Runes,
        Scorpion,
        Wintercoat,
        Weathered,
        Braids = 55
    }

    public enum BanescaleBodyGene
    {
        Basic = 0,
        Cherub = 43,
        Jaguar,
        Pinstripe,
        Tiger,
        Marble,
        Laced,
        Metallic,
        Savannah,
        Petals,
        Skink,
        Poison,
        Chevron,
        Candycane,
        Ragged
    }

    public enum BanescaleWingGene
    {
        Basic = 0,
        Rosette = 44,
        Trail,
        Stripes,
        Edged,
        Alloy,
        Safari,
        Butterfly,
        Spinner,
        Toxin,
        Arrow,
        Sugarplum,
    }

    public enum BanescaleTertGene
    {
        Basic = 0,
        Underbelly = 31,
        Blossom = 36,
        Trimmings = 39,
        Ringlets,
        Fans,
        Squiggle,
        Filigree,
        Lace,
        Skeletal,
        Contour,
        Ghost,
        Wraith,
        Porcupine,
        Crackle,
        Plumage,
    }

    public enum VeilspunBodyGene
    {
        Basic = 0,
        Fade = 60,
        Laced,
        Tapir,
        Vipera,
        Jupiter,
        Starmap,
        Stitched,
        Skink,
        Wasp,
        Bright,
        Arc,
        Shell,
        Sphinxmoth
    }

    public enum VeilspunWingGene
    {
        Basic = 0,
        Bee = 60,
        Blend,
        Edged,
        Striation,
        Hypnotic,
        Saturn,
        Constellation,
        Patchwork,
        Spinner,
        Vivid,
        Loop,
        Web,
        Hawkmoth
    }

    public enum VeilspunTertGene
    {
        Basic = 0,
        Capsule = 56,
        Runes,
        Crackle,
        Okapi,
        Peacock,
        Firefly,
        Opal,
        Branches,
        Flecks,
        Beetle,
        Diaphanous,
        Mop,
        Thorns
    }

    public enum Gender
    {
        Male,
        Female
    }

    public enum Element
    {
        Earth = 1,
        Plague,
        Wind,
        Water,
        Lightning,
        Ice,
        Shadow,
        Light,
        Arcane,
        Nature,
        Fire
    }

    public enum Flight
    {
        Earth,
        Plague,
        Wind,
        Water,
        Lightning,
        Ice,
        Shadow,
        Light,
        Arcane,
        Nature,
        Fire,
        Beastclans
    }

    public enum EyeType
    {
        Common,
        Uncommon,
        Unusual,
        Rare,
        Faceted,
        [Description("Multi-Gaze")]
        MultiGaze,
        Primal,
        Glowing,
        [Description("Dark Sclera")]
        DarkSclera,
        Goat,
        Swirl,
        Innocent
    }

    public enum BodyGene
    {
        Basic,
        Iridescent,
        Tiger,
        Clown,
        Speckle,
        Ripple,
        Bar,
        Crystal,
        Vipera,
        Piebald,
        Cherub,
        Poison,
        Giraffe,
        Petals,
        Jupiter,
        Skink,
        Falcon,
        Metallic,
        Savannah,
        Jaguar,
        Wasp,
        Tapir,
        Pinstripe,
        Python,
        Starmap,
        Lionfish,
        Laced,
        Leopard = 40,
        Slime,
        Fade,
        Swirl = 57,
        Mosaic,
        Stitched
    }

    public enum TertiaryGene
    {
        Basic,
        Circuit,
        Gembond = 4,
        Underbelly,
        Crackle,
        Smoke,
        Spines,
        Okapi,
        Glimmer,
        Thylacine,
        Stained,
        Contour,
        Runes,
        Scales,
        Lace,
        Opal,
        Capsule,
        Smirch,
        Ghost,
        Filigree,
        Firefly,
        Ringlets,
        Peacock,
        Veined = 38,
        Keel = 53,
        Glowtail
    }

    public enum WingGene
    {
        Basic,
        Shimmer,
        Stripes,
        EyeSpots,
        Freckle,
        Seraph,
        Current,
        Daub,
        Facet,
        Hypnotic,
        Paint,
        Peregrine,
        Toxin,
        Butterfly,
        Hex,
        Saturn,
        Spinner,
        Alloy,
        Safari,
        Rosette,
        Bee,
        Striation,
        Trail,
        Morph,
        Noxtide,
        Constellation,
        Edged,
        Clouded = 40,
        Sludge,
        Blend,
        Marbled = 57,
        Breakup,
        Patchwork
    }

    public enum Color
    {
        [Color("#ffffff1a")] Unknown,
        [Color("#fffdea")] Maize,
        [Color("#ffffff")] White,
        [Color("#ebefff")] Ice,
        [Color("#c8bece")] Platinum,
        [Color("#bbbabf")] Silver,
        [Color("#808080")] Grey,
        [Color("#545454")] Charcoal,
        [Color("#4b4946")] Coal,
        [Color("#333333")] Black,
        [Color("#1d2224")] Obsidian,
        [Color("#252735")] Midnight,
        [Color("#3a2e44")] Shadow,
        [Color("#6e235d")] Mulberry,
        [Color("#8f7c8b")] Thistle,
        [Color("#cca4e0")] Lavender,
        [Color("#a261cf")] Purple,
        [Color("#643f9c")] Violet,
        [Color("#4d2c89")] Royal,
        [Color("#757adb")] Storm,
        [Color("#212b5f")] Navy,
        [Color("#324ba9")] Blue,
        [Color("#6392df")] Splash,
        [Color("#aec8ff")] Sky,
        [Color("#7895c1")] Stonewash,
        [Color("#556979")] Steel,
        [Color("#2f4557")] Denim,
        [Color("#0a3d67")] Azure,
        [Color("#0086ce")] Caribbean,
        [Color("#2b768f")] Teal,
        [Color("#72c4c4")] Aqua,
        [Color("#b2e2bd")] Seafoam,
        [Color("#61ab89")] Jade,
        [Color("#20603f")] Emerald,
        [Color("#1e361a")] Jungle,
        [Color("#425035")] Forest,
        [Color("#687f67")] Swamp,
        [Color("#567c34")] Avocado,
        [Color("#629c3f")] Green,
        [Color("#a5e32d")] Leaf,
        [Color("#a9a832")] Spring,
        [Color("#bea55d")] Goldenrod,
        [Color("#ffe63b")] Lemon,
        [Color("#ffec80")] Banana,
        [Color("#ffd297")] Ivory,
        [Color("#e8af49")] Gold,
        [Color("#fa912b")] Sunshine,
        [Color("#d5602b")] Orange,
        [Color("#ef5c23")] Fire,
        [Color("#ff7360")] Tangerine,
        [Color("#b27749")] Sand,
        [Color("#cabba2")] Beige,
        [Color("#827a64")] Stone,
        [Color("#564d48")] Slate,
        [Color("#5a4534")] Soil,
        [Color("#8e5b3f")] Brown,
        [Color("#563012")] Chocolate,
        [Color("#8b3220")] Rust,
        [Color("#ba311c")] Tomato,
        [Color("#850012")] Crimson,
        [Color("#451717")] Blood,
        [Color("#652127")] Maroon,
        [Color("#c1272d")] Red,
        [Color("#b13a3a")] Carmine,
        [Color("#cc6f6f")] Coral,
        [Color("#e934aa")] Magenta,
        [Color("#e77fbf")] Pink,
        [Color("#ffd6f6")] Rose,
        [Color("#9777bd")] Heather,
        [Color("#d950ff")] Orchid,
        [Color("#342b25")] Oilslick,
        [Color("#0d095b")] Sapphire,
        [Color("#4d0f28")] Wine,
        [Color("#9c4875")] Mauve,
        [Color("#d8d7d8")] Moon,
        [Color("#ffb43b")] Marigold,
        [Color("#c49a70")] Tan,
        [Color("#c05a39")] Cinnamon,
        [Color("#148e67")] Spearmint,
        [Color("#99ff9c")] Mantis,
        [Color("#236925")] Shamrock,
        [Color("#1d2715")] Hunter,
        [Color("#535195")] Iris,
        [Color("#b2560d")] Bronze,
        [Color("#ff8400")] Saffron,
        [Color("#fbe9f8")] Pearl,
        [Color("#cd000e")] Ruby,
        [Color("#8b272c")] Berry,
        [Color("#725639")] Hickory,
        [Color("#00fff0")] Cyan,
        [Color("#1c51e7")] Ultramarine,
        [Color("#9494a9")] Smoke,
        [Color("#853390")] Plum,
        [Color("#d1b300")] Honey,
        [Color("#a44b28")] Copper,
        [Color("#6d665a")] Taupe,
        [Color("#0d1e24")] Abyss,
        [Color("#d8d6cd")] Antique,
        [Color("#535264")] Gloom,
        [Color("#9aeaef")] Robin,
        [Color("#8bbbb2")] Spruce,
        [Color("#8ecd55")] Pear,
        [Color("#d0e672")] Honeydew,
        [Color("#c18e1b")] Amber,
        [Color("#f9e255")] Yellow,
        [Color("#ffb576")] Peach,
        [Color("#603f3d")] Clay,
        [Color("#9a534d")] Brick,
        [Color("#b23b07")] Terracotta,
        [Color("#eaa9ff")] Bubblegum,
        [Color("#ebe7ae")] Sanddollar,
        [Color("#332b65")] Eggplant,
        [Color("#2d237a")] Indigo,
        [Color("#7ece73")] Fern,
        [Color("#993bd0")] Amethyst,
        [Color("#7e7745")] Moss,
        [Color("#aa0024")] Cherry,
        [Color("#00b4d6")] Cerulean,
        [Color("#413c3f")] Lead,
        [Color("#724e7b")] Wisteria,
        [Color("#db518d")] Watermelon,
        [Color("#2e0002")] Sanguine,
        [Color("#90532b")] Ginger,
        [Color("#697135")] Olive,
        [Color("#855c32")] Tarnish,
        [Color("#e2ffe6")] Pistachio,
        [Color("#444f69")] Overcast,
        [Color("#4b294f")] Blackberry,
        [Color("#f7ff6f")] Grapefruit,
        [Color("#626268")] Flint,
        [Color("#c6ff00")] Radioactive,
        [Color("#e0dfff")] Orca,
        [Color("#a22929")] Cerise,
        [Color("#ff5500")] Carrot,
        [Color("#1f4739")] Peacock,
        [Color("#4866d5")] Periwinkle,
        [Color("#003484")] Cobalt,
        [Color("#a593b0")] Fog,
        [Color("#57372c")] Sable,
        [Color("#fde9ae")] Flaxen,
        [Color("#d1b046")] Metals,
        [Color("#005e48")] Thicket,
        [Color("#4b4420")] Murk,
        [Color("#977b6c")] Latte,
        [Color("#e8ffb5")] Peridot,
        [Color("#75a8ff")] Cornflower,
        [Color("#9c9c9e")] Dust,
        [Color("#570fc0")] Grape,
        [Color("#2b84ff")] Lapis,
        [Color("#3aa0a1")] Turquoise,
        [Color("#e1ceff")] Mist,
        [Color("#0b2d46")] Phthalo,
        [Color("#9affc7")] Mint,
        [Color("#97af8b")] Algae,
        [Color("#51684c")] Camo,
        [Color("#b4cd3c")] Chartreuse,
        [Color("#c67047")] Caramel,
        [Color("#2f1e1a")] Umber,
        [Color("#ff6840")] Pumpkin,
        [Color("#ffa2a2")] Blush,
        [Color("#8a0249")] Raspberry,
        [Color("#5b0f14")] Garnet,
        [Color("#76483f")] Dirt,
        [Color("#ffefdc")] Cream,
        [Color("#eb7997")] Cottoncandy,
        [Color("#766359")] Driftwood,
        [Color("#7b3c1d")] Auburn,
        [Color("#f6bf6b")] Buttercup,
        [Color("#de3235")] Strawberry,
        [Color("#e22d17")] Vermilion,
        [Color("#ec0089")] Fuchsia,
        [Color("#ff984f")] Cantaloupe,
        [Color("#ffa248")] Sunset,
        [Color("#828335")] Crocodile,
        [Color("#474aa0")] Twilight,
        [Color("#782eb2")] Nightshade,
        [Color("#252a25")] Eldritch,
        [Color("#4d4850")] Shale
    }

    public enum Age
    {
        Hatchling,
        Adult
    }
}