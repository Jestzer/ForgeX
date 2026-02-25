using ForgeX.Core.Halo3;

namespace ForgeX.Core.Halo4;

/// <summary>
/// Maps Halo 4 forge palette quota indices to display names and categories.
/// Palette data extracted from MCC .map cache files using TagDatabaseDump.
/// </summary>
public class Halo4PaletteDatabase : IPaletteDatabase
{
    private readonly List<QuotaInfo> _quotas = new();

    public class QuotaInfo
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string[]? Variants { get; set; }
    }

    public string? GetQuotaName(int quotaIndex)
    {
        if (quotaIndex < 0 || quotaIndex >= _quotas.Count) return null;
        return _quotas[quotaIndex].Name;
    }

    public string? GetVariantName(int quotaIndex, int variantIndex)
    {
        if (quotaIndex < 0 || quotaIndex >= _quotas.Count) return null;
        var q = _quotas[quotaIndex];
        if (q.Variants == null || variantIndex < 0 || variantIndex >= q.Variants.Length)
            return q.Name;
        return q.Variants[variantIndex];
    }

    public string? GetCategory(int quotaIndex)
    {
        if (quotaIndex < 0 || quotaIndex >= _quotas.Count) return null;
        return _quotas[quotaIndex].Category;
    }

    /// <summary>
    /// Creates a palette database for the given Halo 4 map ID.
    /// Returns null for maps without known palette data.
    /// </summary>
    public static Halo4PaletteDatabase? Create(int mapId) => mapId switch
    {
        // MCC forge canvases (post-Thorage, 125/124 entries)
        7000 => Erosion(),
        7100 => Impact(),
        7200 => Ravine(),
        7300 => ForgeIsland(),

        // MCC arena maps with known palettes
        10245 => GrifballCourt(),

        // Xbox 360 original forge canvases (pre-Thorage, 109 entries)
        // Xbox 360 used different map IDs; exact canvas can't be determined from ID alone.
        14100 => Xbox360ForgeCanvas(),

        _ => null,
    };

    private void Add(string category, string name)
    {
        _quotas.Add(new QuotaInfo { Name = name, Category = category });
    }

    private void AddVariants(string category, string groupName, params string[] variants)
    {
        _quotas.Add(new QuotaInfo
        {
            Name = groupName,
            Category = category,
            Variants = variants
        });
    }

    // ================================================================
    //  Common entries shared across all forge canvas maps (indices 0-89)
    // ================================================================

    /// <summary>
    /// Adds the 90 common entries shared across all forge canvas maps.
    /// Weapons (31) + Armor Abilities (8) + Gadgets (9) + Spawning (9) +
    /// Ordnance (3) + Objectives (5) + Dominion (10) + Scenery (7) + Vehicles (8).
    /// </summary>
    private void AddCommonEntries()
    {
        // === Weapons - Human (13 entries, indices 0-12) ===
        Add("Weapon", "Magnum");                    // 0: wep_magnum
        Add("Weapon", "Assault Rifle");             // 1: wep_assault_rifle
        Add("Weapon", "Battle Rifle");              // 2: wep_battle_rifle
        Add("Weapon", "DMR");                       // 3: wep_dmr
        Add("Weapon", "Sniper Rifle");              // 4: wep_sniper_rifle
        Add("Weapon", "Rocket Launcher");           // 5: wep_rocket_launcher
        Add("Weapon", "Shotgun");                   // 6: wep_shotgun
        Add("Weapon", "Sticky Detonator");          // 7: wep_sticky_launcher
        Add("Weapon", "SAW");                       // 8: wep_lmg
        Add("Weapon", "Railgun");                   // 9: wep_rail_gun
        Add("Weapon", "Spartan Laser");             // 10: wep_spartan_laser
        Add("Weapon", "Frag Grenade");              // 11: wep_frag
        Add("Weapon", "Mounted Machine Gun");       // 12: wep_mounted_mg

        // === Weapons - Covenant (11 entries, indices 13-23) ===
        Add("Weapon", "Plasma Pistol");             // 13: wep_plasma_pistol
        Add("Weapon", "Storm Rifle");               // 14: wep_assault_carbine
        Add("Weapon", "Covenant Carbine");          // 15: wep_cov_carbine
        Add("Weapon", "Needler");                   // 16: wep_needler
        Add("Weapon", "Beam Rifle");                // 17: wep_beam_rifle
        Add("Weapon", "Energy Sword");              // 18: wep_energy_sword
        Add("Weapon", "Fuel Rod Cannon");           // 19: wep_fuel_rod
        Add("Weapon", "Gravity Hammer");            // 20: wep_gravity_hammer
        Add("Weapon", "Concussion Rifle");          // 21: wep_concussion_rifle
        Add("Weapon", "Plasma Grenade");            // 22: wep_plasma_grenade
        Add("Weapon", "Plasma Cannon");             // 23: wep_plasma_turret

        // === Weapons - Forerunner (7 entries, indices 24-30) ===
        Add("Weapon", "Boltshot");                  // 24: wep_stasis_pistol
        Add("Weapon", "Suppressor");                // 25: wep_fore_rifle
        Add("Weapon", "SMG");                       // 26: wep_smg
        Add("Weapon", "Scattershot");               // 27: wep_spread_gun
        Add("Weapon", "Binary Rifle");              // 28: wep_fore_sniper_rifle
        Add("Weapon", "Incineration Cannon");       // 29: wep_incineration_launcher
        Add("Weapon", "Pulse Grenade");             // 30: wep_fore_grenade

        // === Armor Abilities (8 entries, indices 31-38) ===
        Add("Armor Ability", "Jetpack");            // 31: aa_jetpack
        Add("Armor Ability", "Thruster Pack");      // 32: aa_thruster
        Add("Armor Ability", "Active Camouflage");  // 33: aa_camo
        Add("Armor Ability", "Hardlight Shield");   // 34: aa_shield
        Add("Armor Ability", "Auto Sentry");        // 35: aa_auto_turret
        Add("Armor Ability", "Promethean Vision");  // 36: aa_fore_vision
        Add("Armor Ability", "Hologram");           // 37: aa_hologram
        Add("Armor Ability", "Regeneration Field"); // 38: aa_regen_field

        // === Gadgets (9 entries, indices 39-47) ===
        AddVariants("Gadget", "Explosives",         // 39: ff_explosives
            "Fusion Coil", "Landmine", "Propane Tank");
        AddVariants("Gadget", "Man Cannons",        // 40: ff_man_cannons
            "Man Cannon", "Man Cannon, Heavy", "Man Cannon, Light",
            "Gravity Lift", "Vehicle Man Cannon");
        AddVariants("Gadget", "Gravity Volumes",    // 41: ff_grav_volumes
            "Gravity Volume 5x5", "Gravity Volume 5x5, Inverted",
            "Gravity Volume 10x10", "Gravity Volume 10x10, Inverted");
        Add("Gadget", "Trait Zone");                // 42: gad_trait_zone
        AddVariants("Gadget", "Teleporters",        // 43: ff_teleporters
            "Receiver Node", "Sender Node", "Two-Way Node");
        AddVariants("Gadget", "Shield Doors",       // 44: ff_shields
            "One Way, Small", "One Way, Medium", "One Way, Large",
            "Shield Door, Small", "Shield Door, Medium", "Shield Door, Large");
        AddVariants("Gadget", "FX",                 // 45: ff_special_fx
            "FX: Colorblind", "FX: Next Gen", "FX: Juicy",
            "FX: Nova", "FX: Olde Timey", "FX: Pen And Ink");
        AddVariants("Gadget", "Toys",               // 46: ff_toys
            "Ball", "Golf Ball", "Kill Ball", "Soccer Ball", "Tin Cup");
        AddVariants("Gadget", "Lights",             // 47: ff_lights
            "Light, Red", "Light, Blue", "Light, Green", "Light, Orange",
            "Light, Purple", "Light, Yellow", "Light, White",
            "Light, Red Flashing", "Light, Yellow Flashing");

        // === Spawning (9 entries, indices 48-56) ===
        Add("Spawning", "Initial Spawn");              // 48: sp_initial_spawn
        Add("Spawning", "Respawn Point");              // 49: sp_respawn_point
        Add("Spawning", "Loadout Camera");             // 50: sp_loadout_camera
        Add("Spawning", "Respawn Zone");               // 51: sp_respawn_zone
        Add("Spawning", "Respawn Zone, Weak");         // 52: sp_respawn_zone_weak
        Add("Spawning", "Anti Respawn Zone");          // 53: sp_respawn_zone_anti
        Add("Spawning", "Anti Respawn Zone, Weak");    // 54: sp_respawn_zone_weak_anti
        AddVariants("Spawning", "Safe Boundary",       // 55: ff_safe_area
            "Safe Boundary", "Soft Safe Boundary");
        AddVariants("Spawning", "Kill Boundary",       // 56: ff_kill_area
            "Kill Boundary", "Soft Kill Boundary");

        // === Ordnance Drops (3 entries, indices 57-59) ===
        Add("Ordnance", "Initial Ordnance");        // 57: ord_initial_drop
        Add("Ordnance", "Random Ordnance");          // 58: ord_random_drop
        Add("Ordnance", "Objective Ordnance");        // 59: ord_objective_drop

        // === Objectives (5 entries, indices 60-64) ===
        Add("Objective", "Flag Stand");              // 60: obj_flag_stand
        Add("Objective", "Capture Plate");           // 61: obj_capture_plate
        Add("Objective", "Hill Marker");             // 62: obj_hill_marker
        Add("Objective", "Decal");                   // 63: ext_decal
        AddVariants("Objective", "Extraction Targets",  // 64: ff_extraction_targets
            "Extraction Crate, Small", "Extraction Crate, Medium", "Extraction Cylinder");

        // === Dominion (10 entries, indices 65-74) ===
        Add("Dominion", "Base Terminal");            // 65: dom_base_terminal
        Add("Dominion", "Turret Pad");               // 66: ff_dom_turretpad
        AddVariants("Dominion", "Dominion Shields",  // 67: ff_dom_shields
            "Shield, Small", "Shield, Medium", "Shield, Large");
        AddVariants("Dominion", "Vehicle Pads",      // 68: ff_dom_veh_pads
            "Ghost Pad", "Banshee Pad", "Wraith Pad", "Mongoose Pad",
            "Warthog Pad", "Rocket Hog Pad", "Gauss Hog Pad",
            "Mantis Pad", "Scorpion Pad");
        AddVariants("Dominion", "Dominion Cover",    // 69: dom_cover
            "Cover, Line", "Cover, Corner", "Cover, Winged");
        Add("Dominion", "Base Decal");               // 70: dom_base_decal
        AddVariants("Dominion", "Base Monitors",     // 71: ff_dom_base_monitors
            "Monitor 3", "Monitor 4 Foot");
        AddVariants("Dominion", "Turret Monitors",   // 72: ff_dom_turret_monitors
            "Monitor 1", "Monitor 2");
        AddVariants("Dominion", "Antennas",          // 73: ff_dom_antennas
            "Antenna, Small", "Antenna, Large");
        AddVariants("Dominion", "Visuals",           // 74: ff_visuals
            "Terminal Battery", "Terminal Case", "Terminal Lights",
            "Terminal Wires 01", "Terminal Wires 02", "Terminal Wires 03",
            "Junction Box");

        // === Scenery (7 entries, indices 75-81) ===
        AddVariants("Scenery", "Barricades",         // 75: ff_barricades
            "Barricade, Small", "Barricade, Large",
            "Jersey Barrier", "Jersey Barrier, Short");
        Add("Scenery", "Camping Stool");             // 76: sc_camping_stool
        Add("Scenery", "Folding Chair");             // 77: sc_folding_chair
        AddVariants("Scenery", "Crates",             // 78: ff_crates
            "Crate, Small", "Crate, Large",
            "Crate, Heavy Small", "Crate, Heavy Large",
            "Container, Small", "Container, Open Small",
            "Container, Large", "Container, Open Large");
        AddVariants("Scenery", "Sandbags",           // 79: ff_sandbags
            "Sandbag Wall", "Sandbag Corner, 90",
            "Sandbag Cap", "Sandbag Pile",
            "Sandbag, Single", "Sandbag, Triple",
            "Sandbags, Terminal");
        Add("Scenery", "Street Cone");               // 80: sc_street_cone
        AddVariants("Scenery", "Pallets",            // 81: ff_pallets
            "Pallet", "Pallet, Large", "Pallet, Metal");

        // === Vehicles (8 entries, indices 82-89) ===
        Add("Vehicle", "Mongoose");                  // 82: veh_mongoose
        AddVariants("Vehicle", "Warthog",            // 83: ff_warthogs
            "Warthog", "Warthog, Gauss", "Warthog, Rocket");
        Add("Vehicle", "Scorpion");                  // 84: veh_scorpion
        Add("Vehicle", "Mantis");                    // 85: veh_mantis
        Add("Vehicle", "Ghost");                     // 86: veh_ghost
        Add("Vehicle", "Wraith");                    // 87: veh_wraith
        Add("Vehicle", "Banshee");                   // 88: veh_banshee
        Add("Vehicle", "Shade Turret");              // 89: veh_shade_turret
    }

    // ================================================================
    //  Common structure entries (indices 91-108, shared across canvases)
    // ================================================================

    /// <summary>
    /// Adds shared structure entries at indices 91-98 and block entries at 99-108.
    /// Index 97 (ff_natural) has map-specific variants passed as parameter.
    /// </summary>
    private void AddCommonStructureEntries(string[] naturalVariants)
    {
        // === Structure (9 entries, indices 91-98) ===
        AddVariants("Structure", "Building Blocks",  // 91: ff_building_blocks
            "Block 1x1", "Block 1x1, Flat", "Block 1x1, Short",
            "Block 1x1, Tall", "Block 1x1, Tall Thin",
            "Block 1x2", "Block 1x4", "Block 2x1, Flat",
            "Block 2x2", "Block 2x2, Flat", "Block 2x2, Short", "Block 2x2, Tall",
            "Block 2x3", "Block 2x4", "Block 3x1, Flat",
            "Block 3x3", "Block 3x3, Flat", "Block 3x3, Short", "Block 3x3, Tall",
            "Block 3x4", "Block 4x4", "Block 4x4, Flat",
            "Block 4x4, Short", "Block 4x4, Tall",
            "Block 5x1, Short", "Block 5x5, Flat");
        AddVariants("Structure", "Bridges & Platforms",  // 92: ff_bridge_plat
            "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Bridge, XLarge",
            "Bridge, Diagonal", "Bridge, Diagonal Small",
            "Dish", "Dish, Open",
            "Corner, 45 Degrees", "Corner, 2x2", "Corner, 4x4",
            "Landing Pad", "Platform, Ramped",
            "Platform, Large", "Platform, XL", "Platform, XXL",
            "Platform, Y", "Platform, Y Large",
            "Sniper Nest", "Staircase", "Walkway, Large");
        AddVariants("Structure", "Buildings",            // 93: ff_buildings
            "Bunker, Small", "Bunker, Small Covered", "Bunker, Box",
            "Bunker, Round", "Bunker, Ramp",
            "Pyramid", "Tower, 2 Story", "Tower, 3 Story", "Tower, Tall",
            "Room, Double", "Room, Triple");
        AddVariants("Structure", "Decorative",           // 94: ff_decorative
            "Antenna, Small", "Antenna, Satellite",
            "Brace", "Brace, Large", "Brace, Tunnel",
            "Column", "Cover", "Cover, Crenellation", "Cover, Glass",
            "Railing, Small", "Railing, Medium", "Railing, Long",
            "Teleporter Frame", "Strut", "Walkway Cover");
        AddVariants("Structure", "Doors, Windows & Walls",  // 95: ff_doors_win_wall
            "Door", "Door, Double", "Window", "Window, Double",
            "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved",
            "Wall, Coliseum", "Window, Coliseum",
            "Tunnel, Short", "Tunnel, Long");
        AddVariants("Structure", "Inclines",             // 96: ff_inclines
            "Bank, 1x1", "Bank, 1x2", "Bank, 2x1", "Bank, 2x2",
            "Ramp, 1x2", "Ramp, 1x2 Shallow",
            "Ramp, 2x2", "Ramp, 2x2 Steep",
            "Ramp, Circular Small", "Ramp, Circular Large",
            "Ramp, Bridge Small", "Ramp, Bridge Medium", "Ramp, Bridge Large",
            "Ramp, XL", "Ramp, Stunt");
        AddVariants("Structure", "Natural", naturalVariants);  // 97: ff_natural (map-specific variants)
        Add("Structure", "Grid");                        // 98: ff_grid

        // === Structure Blocks (10 entries, indices 99-108) ===
        Add("Structure", "Block 1x1");               // 99: bb_1x1
        Add("Structure", "Block 1x2");               // 100: bb_1x2
        Add("Structure", "Block 1x4");               // 101: bb_1x4
        Add("Structure", "Block 2x2");               // 102: bb_2x2
        Add("Structure", "Block 2x3");               // 103: bb_2x3
        Add("Structure", "Block 2x4");               // 104: bb_2x4
        Add("Structure", "Block 3x3");               // 105: bb_3x3
        Add("Structure", "Block 3x4");               // 106: bb_3x4
        Add("Structure", "Block 4x4");               // 107: bb_4x4
        Add("Structure", "Block 5x5, Flat");          // 108: bb_5x5_flat
    }

    // ================================================================
    //  Thorage entries (indices 109+, shared across canvases that have them)
    // ================================================================

    /// <summary>
    /// Adds the thorage entries that are common across forge canvases.
    /// Indices 109-117 on maps with Green Screen, or 109-116 on Forge Island.
    /// </summary>
    private void AddThorageCommonEntries(bool hasGreenScreen)
    {
        // Thorage Gadgets (1 entry)
        AddVariants("Gadget", "Man Cannons (Thorage)",    // 109: ff_man_cannons
            "Monolith Man Cannon", "Daybreak Man Cannon", "Daybreak Gravity Lift");

        // Thorage Vehicles (4 entries)
        Add("Vehicle", "Broadsword");                     // 110: ff_thorage_broadsword
        Add("Vehicle", "Pelican");                        // 111: ff_thorage_pelican
        Add("Vehicle", "Warthog, Unarmed");               // 112: ff_thorage_warthog_unarmed
        AddVariants("Vehicle", "Hidden",                  // 113: ff_thorage_hidden
            "Revenant");

        // Thorage Scenery
        if (hasGreenScreen)
            Add("Scenery", "Green Screen");               // 114: fw_island_greenscreen

        AddVariants("Scenery", "Human Scenery",           // 115/114: ff_thorage_human
            "Jersey Barrier", "Crate, Metal Large", "Crate, Barrels",
            "Crate, Metal Small", "Crate, Small", "Crate, Large",
            "Port Crate, Small 1", "Port Crate, Small 2",
            "Port Crate, Smaller 1", "Port Crate, Smaller 2",
            "Weapon Rack", "Turret Pad", "Missile Battery",
            "Skyline Cover", "UNSC Canister", "Forklift", "Creeper Cone");
        AddVariants("Scenery", "Covenant Scenery",        // 116/115: ff_thorage_covenant
            "Portable Shield", "Covenant Barrier",
            "Crate, Small", "Crate, Large",
            "Container, Small", "Container, Open Small",
            "Weapon Rack", "Turret Pad", "Terminal Screen",
            "Watchtower Base", "Watchtower Pod", "Watchtower Pod, No Guns",
            "Antenna, Small", "Shield, Small", "Phantom", "Lich");
        AddVariants("Scenery", "Forerunner Scenery",      // 117/116: ff_thorage_forerunner
            "Barricade, Small", "Barricade, Large",
            "Crate, Small", "Crate, Heavy Large", "Crate, Heavy Small",
            "Terminal Screen", "Cover", "Cover, Crenellation", "Weapon Rack");
    }

    /// <summary>
    /// Adds the thorage natural entry with Forge Island rock/tree variants.
    /// </summary>
    private void AddThorageNatural(string[] variants)
    {
        AddVariants("Structure", "Natural (Thorage)", variants);
    }

    // ================================================================
    //  Shared thorage structure variant data
    // ================================================================

    private static readonly string[] RavineVariants =
    {
        "Ravine Artifact Base", "Ravine Base 01",
        "Ravine Base 02, Left", "Ravine Base 02, Right",
        "Ravine Bridge Wall", "Ravine Catwalk 01", "Ravine Catwalk 02"
    };

    private static readonly string[] ImpactVariants =
    {
        "Impact Console", "Impact Core",
        "Impact Corridor", "Impact Corridor, Ramp",
        "Impact Corridor, 90", "Impact Corridor, 45",
        "Impact Cap, Large",
        "Impact Corridor, 4-Way Large", "Impact Corridor, 4-Way",
        "Impact Corridor, T", "Impact Corridor, Airlock Door",
        "Impact Corridor, Z-Trans",
        "Impact Corridor, Doorjam", "Impact Corridor, Doorjamb",
        "Impact Corridor, Window Cap",
        "Impact Building 02", "Impact Building 01", "Impact Building 03",
        "Impact Plateau 01", "Impact Plateau 04", "Impact Plateau 04, Closed",
        "Impact Plateau 05", "Impact Cap"
    };

    private static readonly string[] ErosionVariants =
    {
        "Erosion Pipe", "Erosion Pipe, 45",
        "Erosion Pipe, Cross", "Erosion Pipe, Curved",
        "Erosion Pipe, End", "Erosion Pipe, Slant", "Erosion Pipe, Y"
    };

    private static readonly string[] ForgeIslandVariants =
    {
        "Forge Island Artifact Base", "Forge Island Bridge Wall",
        "Forge Island Catwalk 01", "Forge Island Catwalk 02",
        "Forge Island Green Screen"
    };

    private static readonly string[] DropoffVariants =
    {
        "Dropoff Floor", "Dropoff Shield",
        "Dropoff Wall, Back", "Dropoff Wall, Front",
        "Dropoff Catwalk, 2x2", "Dropoff Catwalk, 2x4"
    };

    private static readonly string[] CreeperVariants =
    {
        "Creeper Door, Top", "Creeper Door, Bottom",
        "Creeper Wall, Top", "Creeper Wall, Bottom",
        "Creeper Barrier, Small", "Creeper Barrier, Large",
        "Creeper Catwalk, 2x2", "Creeper Catwalk, 2x4",
        "Creeper Crate, Small", "Creeper Crate, Large"
    };

    private static readonly string[] PortVariants =
    {
        "Port Bridge, Small", "Port Bridge, Medium", "Port Bridge, Large",
        "Port Door, Small", "Port Door, Large",
        "Port Railing, Small", "Port Railing, Large"
    };

    private static readonly string[] ThorageDoorVariants =
    {
        "Door, Double", "Wall", "Door", "Thorage Wall", "Skyline Wall Panel"
    };

    private static readonly string[] ThorageNaturalIslandVariants =
    {
        "Island Rock 01", "Island Rock 02", "Island Rock 03",
        "Island Rock 04", "Island Rock 05",
        "Island Tree 01", "Island Tree 02", "Island Tree 03",
        "Dropoff Rock, Small", "Dropoff Rock, Large"
    };

    // Natural variants that vary per map for the ff_natural thorage entry
    private static readonly string[] ThorageNaturalRavineVariants =
    {
        "Ravine Rock 01", "Ravine Rock 02", "Ravine Rock 03", "Ravine Rock 04",
        "Dropoff Rock, Small", "Dropoff Rock, Large"
    };

    // ================================================================
    //  Per-map database factories
    // ================================================================

    private static Halo4PaletteDatabase Erosion()
    {
        var db = new Halo4PaletteDatabase();
        db.AddCommonEntries();

        // Index 90: Erosion map-specific structure
        AddVariants(db, "Structure", "Erosion", ErosionVariants);

        // Indices 91-108: Common structure entries
        db.AddCommonStructureEntries(new[]
        {
            "Rock, Flat", "Rock, Medium 1", "Rock, Medium 2",
            "Rock, Seastack", "Rock, Small", "Rock, Spire 1", "Rock, Spire 2"
        });

        // Indices 109+: Thorage
        db.AddThorageCommonEntries(hasGreenScreen: true);
        db.AddThorageNatural(ThorageNaturalIslandVariants);

        // Thorage structure (119-124): Ravine, Impact, Dropoff, Creeper, Port, Doors
        AddVariants(db, "Structure", "Ravine Pieces", RavineVariants);
        AddVariants(db, "Structure", "Impact Pieces", ImpactVariants);
        AddVariants(db, "Structure", "Dropoff Base", DropoffVariants);
        AddVariants(db, "Structure", "Creeper Structure", CreeperVariants);
        AddVariants(db, "Structure", "Port Pieces", PortVariants);
        AddVariants(db, "Structure", "Doors & Walls (Thorage)", ThorageDoorVariants);

        return db;
    }

    private static Halo4PaletteDatabase Impact()
    {
        var db = new Halo4PaletteDatabase();
        db.AddCommonEntries();

        // Index 90: Impact (Bonanza) map-specific structure
        AddVariants(db, "Structure", "Impact", ImpactVariants);

        // Indices 91-108: Common structure entries
        db.AddCommonStructureEntries(new[]
        {
            "Rock, Small", "Rock, Flat", "Rock, Medium 1",
            "Rock, Medium 2", "Rock, Spire 1", "Rock, Spire 2", "Rock, Seastack"
        });

        // Indices 109+: Thorage
        db.AddThorageCommonEntries(hasGreenScreen: true);
        db.AddThorageNatural(ThorageNaturalIslandVariants);

        // Thorage structure (119-124): Ravine, Erosion, Dropoff, Creeper, Port, Doors
        AddVariants(db, "Structure", "Ravine Pieces", RavineVariants);
        AddVariants(db, "Structure", "Erosion Pieces", ErosionVariants);
        AddVariants(db, "Structure", "Dropoff Base", DropoffVariants);
        AddVariants(db, "Structure", "Creeper Structure", CreeperVariants);
        AddVariants(db, "Structure", "Port Pieces", PortVariants);
        AddVariants(db, "Structure", "Doors & Walls (Thorage)", ThorageDoorVariants);

        return db;
    }

    private static Halo4PaletteDatabase Ravine()
    {
        var db = new Halo4PaletteDatabase();
        db.AddCommonEntries();

        // Index 90: Ravine map-specific structure
        AddVariants(db, "Structure", "Ravine", RavineVariants);

        // Indices 91-108: Common structure entries
        db.AddCommonStructureEntries(new[]
        {
            "Ravine Rock 01", "Ravine Rock 02", "Ravine Rock 03", "Ravine Rock 04"
        });

        // Indices 109+: Thorage
        db.AddThorageCommonEntries(hasGreenScreen: true);
        db.AddThorageNatural(ThorageNaturalIslandVariants);

        // Thorage structure (119-124): Impact, Erosion, Dropoff, Creeper, Port, Doors
        AddVariants(db, "Structure", "Impact Pieces", ImpactVariants);
        AddVariants(db, "Structure", "Erosion Pieces", ErosionVariants);
        AddVariants(db, "Structure", "Dropoff Base", DropoffVariants);
        AddVariants(db, "Structure", "Creeper Structure", CreeperVariants);
        AddVariants(db, "Structure", "Port Pieces", PortVariants);
        AddVariants(db, "Structure", "Doors & Walls (Thorage)", ThorageDoorVariants);

        return db;
    }

    private static Halo4PaletteDatabase ForgeIsland()
    {
        var db = new Halo4PaletteDatabase();
        db.AddCommonEntries();

        // Index 90: Forge Island map-specific structure
        AddVariants(db, "Structure", "Forge Island", ForgeIslandVariants);

        // Indices 91-108: Common structure entries
        db.AddCommonStructureEntries(new[]
        {
            "Island Rock 01", "Island Rock 02", "Island Rock 03",
            "Island Tree 01", "Island Tree 02", "Island Tree 03"
        });

        // Indices 109+: Thorage (no Green Screen on Forge Island)
        db.AddThorageCommonEntries(hasGreenScreen: false);
        db.AddThorageNatural(ThorageNaturalRavineVariants);

        // Thorage structure (118-123): Impact, Erosion, Dropoff, Creeper, Port, Doors
        AddVariants(db, "Structure", "Impact Pieces", ImpactVariants);
        AddVariants(db, "Structure", "Erosion Pieces", ErosionVariants);
        AddVariants(db, "Structure", "Dropoff Base", DropoffVariants);
        AddVariants(db, "Structure", "Creeper Structure", CreeperVariants);
        AddVariants(db, "Structure", "Port Pieces", PortVariants);
        AddVariants(db, "Structure", "Doors & Walls (Thorage)", ThorageDoorVariants);

        return db;
    }

    private static Halo4PaletteDatabase GrifballCourt()
    {
        var db = new Halo4PaletteDatabase();

        // Grifball Court shares indices 0-64 with forge canvases but has
        // NO dominion entries, different vehicles, and different structure.

        // === Weapons - Human (13 entries, indices 0-12) ===
        db.Add("Weapon", "Magnum");
        db.Add("Weapon", "Assault Rifle");
        db.Add("Weapon", "Battle Rifle");
        db.Add("Weapon", "DMR");
        db.Add("Weapon", "Sniper Rifle");
        db.Add("Weapon", "Rocket Launcher");
        db.Add("Weapon", "Shotgun");
        db.Add("Weapon", "Sticky Detonator");
        db.Add("Weapon", "SAW");
        db.Add("Weapon", "Railgun");
        db.Add("Weapon", "Spartan Laser");
        db.Add("Weapon", "Frag Grenade");
        db.Add("Weapon", "Mounted Machine Gun");

        // === Weapons - Covenant (11 entries) ===
        db.Add("Weapon", "Plasma Pistol");
        db.Add("Weapon", "Storm Rifle");
        db.Add("Weapon", "Covenant Carbine");
        db.Add("Weapon", "Needler");
        db.Add("Weapon", "Beam Rifle");
        db.Add("Weapon", "Energy Sword");
        db.Add("Weapon", "Fuel Rod Cannon");
        db.Add("Weapon", "Gravity Hammer");
        db.Add("Weapon", "Concussion Rifle");
        db.Add("Weapon", "Plasma Grenade");
        db.Add("Weapon", "Plasma Cannon");

        // === Weapons - Forerunner (7 entries) ===
        db.Add("Weapon", "Boltshot");
        db.Add("Weapon", "Suppressor");
        db.Add("Weapon", "SMG");
        db.Add("Weapon", "Scattershot");
        db.Add("Weapon", "Binary Rifle");
        db.Add("Weapon", "Incineration Cannon");
        db.Add("Weapon", "Pulse Grenade");

        // === Armor Abilities (8 entries) ===
        db.Add("Armor Ability", "Jetpack");
        db.Add("Armor Ability", "Thruster Pack");
        db.Add("Armor Ability", "Active Camouflage");
        db.Add("Armor Ability", "Hardlight Shield");
        db.Add("Armor Ability", "Auto Sentry");
        db.Add("Armor Ability", "Promethean Vision");
        db.Add("Armor Ability", "Hologram");
        db.Add("Armor Ability", "Regeneration Field");

        // === Gadgets (9 entries) ===
        db.AddVariants("Gadget", "Explosives",
            "Fusion Coil", "Landmine", "Propane Tank");
        db.AddVariants("Gadget", "Man Cannons",
            "Man Cannon", "Man Cannon, Heavy", "Man Cannon, Light",
            "Gravity Lift", "Vehicle Man Cannon");
        db.AddVariants("Gadget", "Gravity Volumes",
            "Gravity Volume 5x5", "Gravity Volume 5x5, Inverted",
            "Gravity Volume 10x10", "Gravity Volume 10x10, Inverted");
        db.Add("Gadget", "Trait Zone");
        db.AddVariants("Gadget", "Teleporters",
            "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Gadget", "Shield Doors",
            "One Way, Small", "One Way, Medium", "One Way, Large",
            "Shield Door, Small", "Shield Door, Medium", "Shield Door, Large");
        db.AddVariants("Gadget", "FX",
            "FX: Colorblind", "FX: Next Gen", "FX: Juicy",
            "FX: Nova", "FX: Olde Timey", "FX: Pen And Ink");
        db.AddVariants("Gadget", "Toys",
            "Ball", "Golf Ball", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Gadget", "Lights",
            "Light, Red", "Light, Blue", "Light, Green", "Light, Orange",
            "Light, Purple", "Light, Yellow", "Light, White",
            "Light, Red Flashing", "Light, Yellow Flashing");

        // === Spawning (9 entries) ===
        db.Add("Spawning", "Initial Spawn");
        db.Add("Spawning", "Respawn Point");
        db.Add("Spawning", "Loadout Camera");
        db.Add("Spawning", "Respawn Zone");
        db.Add("Spawning", "Respawn Zone, Weak");
        db.Add("Spawning", "Anti Respawn Zone");
        db.Add("Spawning", "Anti Respawn Zone, Weak");
        db.AddVariants("Spawning", "Safe Boundary",
            "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawning", "Kill Boundary",
            "Kill Boundary", "Soft Kill Boundary");

        // === Ordnance Drops (3 entries) ===
        db.Add("Ordnance", "Initial Ordnance");
        db.Add("Ordnance", "Random Ordnance");
        db.Add("Ordnance", "Objective Ordnance");

        // === Objectives (5 entries) ===
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.Add("Objective", "Decal");
        db.AddVariants("Objective", "Extraction Targets",
            "Extraction Crate, Small", "Extraction Crate, Medium", "Extraction Cylinder");

        // === Scenery (7 entries) — NO dominion on Grifball Court ===
        db.AddVariants("Scenery", "Barricades",
            "Barricade, Small", "Barricade, Large",
            "Jersey Barrier", "Jersey Barrier, Short");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crates",
            "Crate, Small", "Crate, Large",
            "Crate, Heavy Small", "Crate, Heavy Large",
            "Container, Small", "Container, Open Small",
            "Container, Large", "Container, Open Large");
        db.AddVariants("Scenery", "Sandbags",
            "Sandbag Wall", "Sandbag Corner, 90",
            "Sandbag Cap", "Sandbag Pile",
            "Sandbag, Single", "Sandbag, Triple",
            "Sandbags, Terminal");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallets",
            "Pallet", "Pallet, Large", "Pallet, Metal");

        // === Vehicles (2 entries only) ===
        db.Add("Vehicle", "Mongoose");               // 82: veh_mongoose
        db.Add("Vehicle", "Ghost");                   // 83: veh_ghost

        // === Structure (16 entries) ===
        db.Add("Structure", "Block 1x1");
        db.Add("Structure", "Block 1x2");
        db.Add("Structure", "Block 1x4");
        db.Add("Structure", "Block 2x2");
        db.Add("Structure", "Block 2x3");
        db.Add("Structure", "Block 2x4");
        db.Add("Structure", "Block 3x3");
        db.Add("Structure", "Block 3x4");
        db.Add("Structure", "Block 4x4");
        db.Add("Structure", "Block 5x5, Flat");
        db.Add("Structure", "Forerunner Bridge, Large");
        db.Add("Structure", "Forerunner Cover, Small");
        db.Add("Structure", "Forerunner Cover, Small 01");
        db.Add("Structure", "Forerunner Door Seal");
        db.Add("Structure", "Forerunner Gravity Lift");
        db.Add("Structure", "Forerunner Ramp Block");

        return db;
    }

    /// <summary>
    /// Xbox 360 original Halo 4 forge canvas palette (pre-Thorage, 109 entries).
    /// Entries 0-89 and 91-108 are identical across all canvases. Entry 90 uses
    /// a generic label since the exact canvas can't be determined from the map ID alone.
    /// </summary>
    private static Halo4PaletteDatabase Xbox360ForgeCanvas()
    {
        var db = new Halo4PaletteDatabase();
        db.AddCommonEntries();

        // Index 90: Map-specific structure (unknown canvas, use generic name)
        db.Add("Structure", "Map Structure");

        // Indices 91-108: Common structure entries (use generic natural variants)
        db.AddCommonStructureEntries(new[] { "Natural" });

        // No thorage section on Xbox 360 (pre-MCC Thorage update)
        return db;
    }

    /// <summary>
    /// Static helper to add variants to a database instance (used by per-map factories).
    /// </summary>
    private static void AddVariants(Halo4PaletteDatabase db, string category, string displayName, string[] variants)
    {
        db._quotas.Add(new QuotaInfo
        {
            Name = displayName,
            Category = category,
            Variants = variants
        });
    }
}
