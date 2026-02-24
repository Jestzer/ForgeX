namespace ForgeX.Core.Reach;

/// <summary>
/// Maps Reach forge palette quota indices to display names and categories.
/// Palette data sourced from Mjolnir Forge Editor (MIT license).
/// https://github.com/Waffle1434/Mjolnir-Forge-Editor
/// </summary>
public class ReachPaletteDatabase
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
    /// Creates a palette database for the given Reach map ID.
    /// Returns null for maps without known palette data.
    /// </summary>
    public static ReachPaletteDatabase? Create(int mapId) => mapId switch
    {
        3006 => ForgeWorld(),
        1520 => Tempest(),
        2006 or 10030 => Ridgeline(),        // Ridgeline + Timberland Anniversary
        2009 or 10050 => Breakneck(),        // Breakneck + Headlong Anniversary
        2002 => Highlands(),
        1055 => Reflection(),
        1150 => SwordBase(),
        1080 => Spire(),
        1000 => Boardwalk(),
        1020 => Boneyard(),
        1035 => Countdown(),
        1040 => Powerhouse(),
        1200 => Zealot(),
        1500 => Anchor9(),
        1510 => Breakpoint(),
        2001 => Condemned(),
        2004 or 10020 => BattleCanyon(),     // Battle Canyon + Beaver Creek Anniversary
        2005 or 10010 => Penance(),          // Penance + Damnation Anniversary
        2007 or 10070 => Solitary(),         // Solitary + Prisoner Anniversary
        2008 or 10060 => HighNoon(),         // High Noon + Hang 'Em High Anniversary
        _ => null,
    };

    private void Add(string category, string name)
    {
        _quotas.Add(new QuotaInfo { Name = name, Category = category });
    }

    private void AddVariants(string category, params string[] variants)
    {
        _quotas.Add(new QuotaInfo
        {
            Name = variants[0],
            Category = category,
            Variants = variants
        });
    }

    /// <summary>
    /// Adds the 31 universal entries shared across all maps (indices 0-30).
    /// 10 human weapons + 14 covenant weapons + 7 armor abilities.
    /// </summary>
    private void AddUniversalEntries()
    {
        // Weapons Human (10)
        Add("Weapon", "Assault Rifle");
        Add("Weapon", "DMR");
        Add("Weapon", "Grenade Launcher");
        Add("Weapon", "Magnum");
        Add("Weapon", "Rocket Launcher");
        Add("Weapon", "Shotgun");
        Add("Weapon", "Sniper Rifle");
        Add("Weapon", "Spartan Laser");
        Add("Weapon", "Frag Grenade");
        Add("Weapon", "Mounted Machinegun");

        // Weapons Covenant (14)
        Add("Weapon", "Concussion Rifle");
        Add("Weapon", "Energy Sword");
        Add("Weapon", "Fuel Rod Gun");
        Add("Weapon", "Gravity Hammer");
        Add("Weapon", "Focus Rifle");
        Add("Weapon", "Needle Rifle");
        Add("Weapon", "Needler");
        Add("Weapon", "Plasma Launcher");
        Add("Weapon", "Plasma Pistol");
        Add("Weapon", "Plasma Repeater");
        Add("Weapon", "Plasma Rifle");
        Add("Weapon", "Spiker");
        Add("Weapon", "Plasma Grenade");
        Add("Weapon", "Plasma Turret");

        // Armor Abilities (7)
        Add("Armor Ability", "Active Camouflage");
        Add("Armor Ability", "Armor Lock");
        Add("Armor Ability", "Drop Shield");
        Add("Armor Ability", "Evade");
        Add("Armor Ability", "Hologram");
        Add("Armor Ability", "Jet Pack");
        Add("Armor Ability", "Sprint");
    }

    private static ReachPaletteDatabase ForgeWorld()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Vehicles
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Falcon");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.Add("Vehicle", "Revenant");
        db.Add("Vehicle", "Scorpion");
        db.Add("Vehicle", "Shade Turret");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.Add("Equipment", "One Way Shield 2");
        db.Add("Equipment", "One Way Shield 3");
        db.Add("Equipment", "One Way Shield 4");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink", "FX:Purple", "FX:Green", "FX:Orange");
        db.Add("Equipment", "Shield Door, Small");
        db.Add("Equipment", "Shield Door, Medium");
        db.Add("Equipment", "Shield Door, Large");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.AddVariants("Scenery", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");

        // Structure > Building Blocks
        db.AddVariants("Structure", "Block, 1x1", "Block, 1x1, Flat", "Block, 1x1, Short", "Block, 1x1, Tall", "Block, 1x1, Tall And Thin", "Block, 1x2", "Block, 1x4", "Block, 2x1, Flat", "Block, 2x2", "Block, 2x2, Flat", "Block, 2x2, Short", "Block, 2x2, Tall", "Block, 2x3", "Block, 2x4", "Block, 3x1, Flat", "Block, 3x3", "Block, 3x3, Flat", "Block, 3x3, Short", "Block, 3x3, Tall", "Block, 3x4", "Block, 4x4", "Block, 4x4, Flat", "Block, 4x4, Short", "Block, 4x4, Tall", "Block, 5x1, Short", "Block, 5x5, Flat");

        // Structure > Bridges And Platforms
        db.AddVariants("Structure", "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Bridge, XLarge", "Bridge, Diagonal", "Bridge, Diag, Small", "Dish", "Dish, Open", "Corner, 45 Degrees", "Corner, 2x2", "Corner, 4x4", "Landing Pad", "Platform, Ramped", "Platform, Large", "Platform, XL", "Platform, XXL", "Platform, Y", "Platform, Y, Large", "Sniper Nest", "Staircase", "Walkway, Large");

        // Structure > Buildings
        db.AddVariants("Structure", "Bunker, Small", "Bunker, Small, Covered", "Bunker, Box", "Bunker, Round", "Bunker, Ramp", "Pyramid", "Tower, 2 Story", "Tower, 3 Story", "Tower, Tall", "Room, Double", "Room, Triple");

        // Structure > Decorative
        db.AddVariants("Structure", "Antenna, Small", "Antenna, Satellite", "Brace", "Brace, Large", "Brace, Tunnel", "Column", "Cover", "Cover, Crenellation", "Cover, Glass", "Glass Sail", "Railing, Small", "Railing, Medium", "Railing, Long", "Teleporter Frame", "Strut", "Large Walkway Cover");

        // Structure > Doors, Windows, And Walls
        db.AddVariants("Structure", "Door", "Door, Double", "Window", "Window, Double", "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved", "Wall, Coliseum", "Window, Colesium", "Tunnel, Short", "Tunnel, Long");

        // Structure > Inclines
        db.AddVariants("Structure", "Bank, 1x1", "Bank, 1x2", "Bank, 2x1", "Bank, 2x2", "Ramp, 1x2", "Ramp, 1x2, Shallow", "Ramp, 2x2", "Ramp, 2x2, Steep", "Ramp, Circular, Small", "Ramp, Circular, Large", "Ramp, Bridge, Small", "Ramp, Bridge, Medium", "Ramp, Bridge, Large", "Ramp, XL", "Ramp, Stunt");

        // Structure > Natural
        db.AddVariants("Structure", "Rock, Small", "Rock, Flat", "Rock, Medium 1", "Rock, Medium 2", "Rock, Spire 1", "Rock, Spire 2", "Rock, Seastack", "Rock, Arch");

        // Structure (end)
        db.Add("Structure", "Grid");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");

        // Vehicles (MCC)
        db.AddVariants("Vehicle", "Falcon, Nose Gun", "Falcon, Grenadier", "Falcon, Transport");
        db.Add("Vehicle", "Warthog, Transport");
        db.Add("Vehicle", "Sabre");
        db.Add("Vehicle", "Seraph");
        db.Add("Vehicle", "Cart, Electric");
        db.Add("Vehicle", "Forklift");
        db.Add("Vehicle", "Pickup");
        db.Add("Vehicle", "Truck Cab");
        db.Add("Vehicle", "Van, Oni");
        db.Add("Vehicle", "Shade, Fuel Rod");

        // Gadgets (MCC)
        db.AddVariants("Equipment", "Cannon, Man, Forerunner", "Cannon, Man, Heavy, Forerunner", "Cannon, Man, Light, Forerunner", "Gravity Lift, Forerunner", "Gravity Lift, Tall, Forerunner", "Cannon, Man, Human");
        db.Add("Equipment", "One Way Shield 1");
        db.Add("Equipment", "One Way Shield 5");
        db.Add("Equipment", "Shield Wall, Small");
        db.Add("Equipment", "Shield Wall, Medium");
        db.Add("Equipment", "Shield Wall, Large");
        db.Add("Equipment", "Shield Wall, X-Large");
        db.Add("Equipment", "One Way Shield 2");
        db.Add("Equipment", "One Way Shield 3");
        db.Add("Equipment", "One Way Shield 4");
        db.Add("Equipment", "Shield Door, Small");
        db.Add("Equipment", "Shield Door, Small 1");
        db.Add("Equipment", "Shield Door, Large");
        db.Add("Equipment", "Shield Door, Large 1");
        db.Add("Equipment", "Ammo Cabinet");
        db.Add("Equipment", "Spnkr Ammo");
        db.Add("Equipment", "Sniper Ammo");

        // Scenery (MCC)
        db.AddVariants("Scenery", "Jersey Barrier", "Jersey Barrier, Short", "Heavy Barrier");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Fully Open", "Crate, Forerunner, Small", "Crate, Forerunner, Large");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.AddVariants("Scenery", "Driftwood 1", "Driftwood 2", "Driftwood 3");
        db.AddVariants("Scenery", "Phantom", "Spirit", "Pelican", "Drop Pod, Elite", "Anti Air Gun");
        db.AddVariants("Scenery", "Cargo Truck, Destroyed", "Falcon, Destroyed", "Warthog, Destroyed");
        db.Add("Scenery", "Folding Chair");
        db.Add("Scenery", "Dumpster");
        db.Add("Scenery", "Dumpster, Tall");
        db.Add("Scenery", "Equipment Case");
        db.Add("Scenery", "Monitor");
        db.Add("Scenery", "Plasma Storage");
        db.Add("Scenery", "Camping Stool, Covenant");
        db.Add("Scenery", "Covenant Antenna");
        db.Add("Scenery", "Fuel Storage");
        db.Add("Scenery", "Engine Cart");
        db.Add("Scenery", "Missile Cart");

        // Structure (MCC)
        db.AddVariants("Structure", "Bridge", "Platform, Covenant", "Catwalk, Straight", "Catwalk, Short", "Catwalk, Bend, Left", "Catwalk, Bend, Right", "Catwalk, Angled", "Catwalk, Large");
        db.AddVariants("Structure", "Bunker, Overlook", "Gunners Nest");
        db.AddVariants("Structure", "Cover, Small", "Block, Large", "Blocker, Hallway", "Column, Stone", "Tombstone", "Cover, Large, Stone", "Cover, Large", "Walkway Cover", "Walkway Cover, Short", "Cover, Large, Human", "I-Beam");
        db.AddVariants("Structure", "Wall (MCC)", "Door (MCC)", "Door, Human", "Door A, Forerunner", "Door B, Forerunner", "Door C, Forerunner", "Door D, Forerunner", "Door E, Forerunner", "Door F, Forerunner", "Door G, Forerunner", "Door H, Forerunner", "Wall, Small, Forerunner", "Wall, Large, Forerunner");
        db.AddVariants("Structure", "Rock, Spire 3", "Tree, Dead");

        // Hidden Misc
        db.Add("Scenery", "Generator");
        db.Add("Scenery", "Vending Machine");
        db.Add("Scenery", "Dinghy");

        // Other (MCC)
        db.Add("Equipment", "Target Designator");
        db.Add("Equipment", "Pelican, Hovering");
        db.Add("Equipment", "Phantom, Hovering");
        db.Add("Objective", "Location Name");

        return db;
    }

    private static ReachPaletteDatabase Tempest()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Vehicles
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");
        db.Add("Vehicle", "Scorpion");

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man, Forerunner", "Cannon, Man, Heavy, Forerunner", "Cannon, Man, Light, Forerunner", "Cannon, Vehicle", "Gravity Lift");
        db.Add("Equipment", "One Way Shield 2");
        db.Add("Equipment", "One Way Shield 3");
        db.Add("Equipment", "One Way Shield 4");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink", "FX:Purple", "FX:Green", "FX:Orange");
        db.Add("Equipment", "Shield Door, Small");
        db.Add("Equipment", "Shield Door, Medium");
        db.Add("Equipment", "Shield Door, Large");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Covenant Barrier");
        db.AddVariants("Scenery", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Driftwood 1", "Driftwood 2", "Driftwood 3");

        // Structure > Building Blocks
        db.AddVariants("Structure", "Block, 1x1", "Block, 1x1, Flat", "Block, 1x1, Short", "Block, 1x1, Tall", "Block, 1x1, Tall And Thin", "Block, 1x2", "Block, 1x4", "Block, 2x1, Flat", "Block, 2x2", "Block, 2x2, Flat", "Block, 2x2, Short", "Block, 2x2, Tall", "Block, 2x3", "Block, 2x4", "Block, 3x1, Flat", "Block, 3x3", "Block, 3x3, Flat", "Block, 3x3, Short", "Block, 3x3, Tall", "Block, 3x4", "Block, 4x4", "Block, 4x4, Flat", "Block, 4x4, Short", "Block, 4x4, Tall", "Block, 5x1, Short", "Block, 5x5, Flat");

        // Structure > Bridges And Platforms
        db.AddVariants("Structure", "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Bridge, XLarge", "Bridge, Diagonal", "Bridge, Diag, Small", "Dish", "Dish, Open", "Corner, 45 Degrees", "Corner, 2x2", "Corner, 4x4", "Landing Pad", "Platform, Ramped", "Platform, Large", "Platform, XL", "Platform, XXL", "Platform, Y", "Platform, Y, Large", "Sniper Nest", "Staircase", "Walkway, Large");

        // Structure > Buildings
        db.AddVariants("Structure", "Bunker, Small", "Bunker, Small, Covered", "Bunker, Box", "Bunker, Round", "Bunker, Ramp", "Pyramid", "Tower, 2 Story", "Tower, 3 Story", "Tower, Tall", "Room, Double", "Room, Triple");

        // Structure > Decorative
        db.AddVariants("Structure", "Antenna, Small", "Antenna, Satellite", "Brace", "Brace, Large", "Brace, Tunnel", "Column", "Cover", "Cover, Crenellation", "Cover, Glass", "Glass Sail", "Railing, Small", "Railing, Medium", "Railing, Long", "Teleporter Frame", "Strut", "Large Walkway Cover");

        // Structure > Doors, Windows, And Walls
        db.AddVariants("Structure", "Door", "Door, Double", "Window", "Window, Double", "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved", "Wall, Coliseum", "Window, Colesium", "Tunnel, Short", "Tunnel, Long");

        // Structure > Inclines
        db.AddVariants("Structure", "Bank, 1x1", "Bank, 1x2", "Bank, 2x1", "Bank, 2x2", "Ramp, 1x2", "Ramp, 1x2, Shallow", "Ramp, 2x2", "Ramp, 2x2, Steep", "Ramp, Circular, Small", "Ramp, Circular, Large", "Ramp, Bridge, Small", "Ramp, Bridge, Medium", "Ramp, Bridge, Large", "Ramp, XL", "Ramp, Stunt");

        // Structure > Natural
        db.AddVariants("Structure", "Rock, Small", "Rock, Flat", "Rock, Medium 1", "Rock, Medium 2", "Rock, Spire 1", "Rock, Spire 2", "Rock, Arch", "Rock, Small 1", "Rock, Spire 3");

        // Structure (end)
        db.Add("Structure", "Grid");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Structure", "Destination Delta(?!)");

        // Vehicles (MCC)
        db.AddVariants("Vehicle", "Falcon, Nose Gun", "Falcon, Grenadier", "Falcon, Transport");
        db.Add("Vehicle", "Warthog, Transport");
        db.Add("Vehicle", "Sabre");
        db.Add("Vehicle", "Seraph");
        db.Add("Vehicle", "Cart, Electric");
        db.Add("Vehicle", "Forklift");
        db.Add("Vehicle", "Pickup");
        db.Add("Vehicle", "Truck Cab");
        db.Add("Vehicle", "Van, Oni");
        db.Add("Vehicle", "Shade, Fuel Rod");

        // Gadgets (MCC)
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Gravity Lift, Forerunner", "Gravity Lift, Tall, Forerunner", "Cannon, Man, Human");
        db.Add("Equipment", "One Way Shield 1");
        db.Add("Equipment", "One Way Shield 5");
        db.Add("Equipment", "Shield Wall, Small");
        db.Add("Equipment", "Shield Wall, Medium");
        db.Add("Equipment", "Shield Wall, Large");
        db.Add("Equipment", "Shield Wall, X-Large");
        db.Add("Equipment", "One Way Shield 2");
        db.Add("Equipment", "One Way Shield 3");
        db.Add("Equipment", "One Way Shield 4");
        db.Add("Equipment", "Shield Door, Small");
        db.Add("Equipment", "Shield Door, Small 1");
        db.Add("Equipment", "Shield Door, Large");
        db.Add("Equipment", "Shield Door, Large 1");
        db.Add("Equipment", "Ammo Cabinet");
        db.Add("Equipment", "Spnkr Ammo");
        db.Add("Equipment", "Sniper Ammo");

        // Scenery (MCC)
        db.AddVariants("Scenery", "Jersey Barrier", "Jersey Barrier, Short", "Heavy Barrier");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Fully Open", "Crate, Forerunner, Small", "Crate, Forerunner, Large");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.AddVariants("Scenery", "Phantom", "Spirit", "Pelican", "Drop Pod, Elite", "Anti Air Gun");
        db.AddVariants("Scenery", "Cargo Truck, Destroyed", "Falcon, Destroyed", "Warthog, Destroyed");
        db.Add("Scenery", "Folding Chair");
        db.Add("Scenery", "Dumpster");
        db.Add("Scenery", "Dumpster, Tall");
        db.Add("Scenery", "Equipment Case");
        db.Add("Scenery", "Monitor");
        db.Add("Scenery", "Plasma Storage");
        db.Add("Scenery", "Camping Stool, Covenant");
        db.Add("Scenery", "Covenant Antenna");
        db.Add("Scenery", "Fuel Storage");
        db.Add("Scenery", "Engine Cart");
        db.Add("Scenery", "Missile Cart");

        // Structure (MCC)
        db.AddVariants("Structure", "Bridge", "Platform, Covenant", "Catwalk, Straight", "Catwalk, Short", "Catwalk, Bend, Left", "Catwalk, Bend, Right", "Catwalk, Angled", "Catwalk, Large");
        db.AddVariants("Structure", "Bunker, Overlook", "Gunners Nest");
        db.AddVariants("Structure", "Cover, Small", "Block, Large", "Blocker, Hallway", "Column, Stone", "Tombstone", "Cover, Large, Stone", "Cover, Large", "Walkway Cover", "Walkway Cover, Short", "Cover, Large, Human", "I-Beam");
        db.AddVariants("Structure", "Wall (MCC)", "Door (MCC)", "Door, Human", "Door A, Forerunner", "Door B, Forerunner", "Door C, Forerunner", "Door D, Forerunner", "Door E, Forerunner", "Door F, Forerunner", "Door G, Forerunner", "Door H, Forerunner", "Wall, Small, Forerunner", "Wall, Large, Forerunner");
        db.Add("Structure", "Tree, Dead");

        // Hidden Misc
        db.Add("Scenery", "Generator");
        db.Add("Scenery", "Vending Machine");
        db.Add("Scenery", "Dinghy");

        // Other (MCC)
        db.Add("Equipment", "Target Designator");
        db.Add("Equipment", "Pelican, Hovering");
        db.Add("Equipment", "Phantom, Hovering");
        db.Add("Objective", "Location Name");

        return db;
    }

    private static ReachPaletteDatabase Ridgeline()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");

        // Vehicles
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Falcon");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.Add("Vehicle", "Revenant");
        db.Add("Vehicle", "Scorpion");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");
        db.Add("Vehicle", "Shade Turret");

        // Structure > Building Blocks
        db.AddVariants("Structure", "Block, 1x1", "Block, 1x1, Flat", "Block, 1x1, Short", "Block, 1x1, Tall", "Block, 1x1, Tall And Thin", "Block, 1x2", "Block, 1x4", "Block, 2x1, Flat", "Block, 2x2", "Block, 2x2, Flat", "Block, 2x2, Short", "Block, 2x2, Tall", "Block, 2x3", "Block, 2x4", "Block, 3x1, Flat", "Block, 3x3", "Block, 3x3, Flat", "Block, 3x3, Short", "Block, 3x3, Tall", "Block, 3x4", "Block, 4x4", "Block, 4x4, Flat", "Block, 4x4, Short", "Block, 4x4, Tall", "Block, 5x1, Short", "Block, 5x5, Flat");

        // Structure > Bridges And Platforms
        db.AddVariants("Structure", "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Bridge, XLarge", "Bridge, Diagonal", "Bridge, Diag, Small", "Corner, 45 Degrees", "Corner, 2x2", "Corner, 4x4", "Landing Pad", "Platform, Ramped", "Platform, Large", "Platform, XL", "Platform, Y", "Platform, Y, Large", "Sniper Nest", "Walkway, Large");

        // Structure > Buildings
        db.AddVariants("Structure", "Bunker, Small", "Bunker, Small, Covered", "Bunker, Box", "Bunker, Ramp", "Tower, 2 Story", "Tower, 3 Story", "Tower, Tall", "Room, Double", "Bunker, Overlook", "Gunner's Nest");

        // Structure > Decorative
        db.AddVariants("Structure", "Antenna, Small", "Brace", "Column", "Cover", "Cover, Crenellation", "Railing, Small", "Railing, Medium", "Railing, Long", "Teleporter Frame", "Strut", "Large Walkway Cover", "Cover, Small");

        // Structure > Doors, Windows, And Walls
        db.AddVariants("Structure", "Door", "Door, Double", "Window", "Window, Double", "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved", "Tunnel, Short", "Tunnel, Long");

        // Structure > Inclines
        db.AddVariants("Structure", "Ramp, 1x2", "Ramp, 1x2, Shallow", "Ramp, 2x2", "Ramp, 2x2, Steep", "Ramp, Circular, Small", "Ramp, Bridge, Small", "Ramp, Bridge, Medium", "Ramp, Bridge, Large", "Ramp, XL");

        // Structure > Natural
        db.AddVariants("Structure", "Rock, Small", "Rock, Flat", "Rock, Medium 1", "Rock, Medium 2", "Rock, Spire 1", "Rock, Spire 2", "Rock, Seastack", "Rock, Arch");

        // Structure (end)
        db.Add("Structure", "Grid");
        db.Add("Structure", "Tree, Dead");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");

        // (end of structure)
        db.Add("Equipment", "Health Cabinet");

        return db;
    }

    private static ReachPaletteDatabase Breakneck()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Man, Human", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");

        // Vehicles
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.Add("Vehicle", "Revenant");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");
        db.Add("Vehicle", "Falcon");
        db.Add("Vehicle", "Scorpion");

        // Structure > Building Blocks
        db.AddVariants("Structure", "Block, 1x1", "Block, 1x1, Flat", "Block, 1x1, Short", "Block, 1x1, Tall", "Block, 1x1, Tall And Thin", "Block, 1x2", "Block, 1x4", "Block, 2x1, Flat", "Block, 2x2", "Block, 2x2, Flat", "Block, 2x2, Short", "Block, 2x2, Tall");

        // Structure > Bridges And Platforms
        db.AddVariants("Structure", "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Bridge, XLarge", "Bridge, Diagonal", "Bridge, Diag, Small", "Corner, 45 Degrees", "Corner, 2x2", "Corner, 4x4");

        // Structure > Buildings
        db.AddVariants("Structure", "Bunker, Small", "Bunker, Small, Covered");

        // Structure > Decorative
        db.AddVariants("Structure", "Antenna, Small", "Column", "Railing, Small", "Railing, Medium", "Railing, Long", "Teleporter Frame", "Strut", "Cover, Large", "I-Beam");

        // Structure > Doors, Windows, And Walls
        db.AddVariants("Structure", "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved", "Door, Human");

        // Structure > Inclines
        db.AddVariants("Structure", "Ramp, 1x2", "Ramp, 1x2, Shallow", "Ramp, 2x2", "Ramp, 2x2, Steep", "Ramp, Circular, Small", "Ramp, Circular, Large", "Ramp, Bridge, Small", "Ramp, Bridge, Medium", "Ramp, Bridge, Large");

        // Structure (end)
        db.Add("Structure", "Grid");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");

        // (end hidden)
        db.Add("Equipment", "Health Cabinet");

        // Vehicles (MCC)
        db.AddVariants("Vehicle", "Falcon, Nose Gun", "Falcon, Grenadier", "Falcon, Transport");
        db.Add("Vehicle", "Warthog, Transport");
        db.Add("Vehicle", "Cart, Electric");
        db.Add("Vehicle", "Forklift");
        db.Add("Vehicle", "Pickup");
        db.Add("Vehicle", "Truck Cab");
        db.Add("Vehicle", "Van, Oni");
        db.Add("Vehicle", "Shade, Fuel Rod");

        // Gadgets (MCC)
        db.Add("Equipment", "One Way Shield 1");
        db.Add("Equipment", "One Way Shield 2");
        db.Add("Equipment", "One Way Shield 3");
        db.Add("Equipment", "One Way Shield 4");
        db.Add("Equipment", "One Way Shield 5");
        db.Add("Equipment", "Shield Wall, Small");
        db.Add("Equipment", "Shield Wall, Medium");
        db.Add("Equipment", "Shield Wall, Large");
        db.Add("Equipment", "Shield Wall, X-Large");
        db.Add("Equipment", "Ammo Cabinet");
        db.Add("Equipment", "Spnkr Ammo");
        db.Add("Equipment", "Sniper Ammo");

        // Scenery (MCC)
        db.Add("Scenery", "Heavy Barrier");
        db.AddVariants("Scenery", "Phantom", "Spirit", "Pelican", "Drop Pod, Elite", "Anti Air Gun");
        db.AddVariants("Scenery", "Cargo Truck, Destroyed", "Falcon, Destroyed", "Warthog, Destroyed");
        db.Add("Scenery", "Folding Chair");
        db.Add("Scenery", "Dumpster");
        db.Add("Scenery", "Dumpster, Tall");
        db.Add("Scenery", "Equipment Case");
        db.Add("Scenery", "Monitor");
        db.Add("Scenery", "Plasma Storage");
        db.Add("Scenery", "Camping Stool, Covenant");
        db.Add("Scenery", "Covenant Antenna");
        db.Add("Scenery", "Fuel Storage");
        db.Add("Scenery", "Engine Cart");
        db.Add("Scenery", "Missile Cart");

        // Structure (MCC)
        db.AddVariants("Structure", "Wall (MCC)", "Door (MCC)");

        return db;
    }

    private static ReachPaletteDatabase Highlands()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Vehicles
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Falcon");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.Add("Vehicle", "Revenant");
        db.Add("Vehicle", "Scorpion");
        db.Add("Vehicle", "Shade Turret");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Dumpster, Tall");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");

        return db;
    }

    private static ReachPaletteDatabase Reflection()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Dumpster, Tall");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");

        return db;
    }

    private static ReachPaletteDatabase SwordBase()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");

        return db;
    }

    private static ReachPaletteDatabase Spire()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();

        // Vehicles
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Falcon");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.Add("Vehicle", "Revenant");
        db.Add("Vehicle", "Scorpion");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");

        // Gadgets
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.Add("Equipment", "Golf Club");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange");

        // Spawning
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");

        // Objectives
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");

        // Scenery
        db.Add("Scenery", "Ramp, Stunt");
        db.Add("Scenery", "Covenant Barrier");
        db.Add("Scenery", "Shield Door, Small");
        db.Add("Scenery", "Shield Door, Medium");
        db.Add("Scenery", "Shield Door, Large");
        db.Add("Scenery", "One Way Shield 2");
        db.Add("Scenery", "One Way Shield 3");
        db.Add("Scenery", "One Way Shield 4");
        db.Add("Scenery", "Shield Wall, Small");
        db.Add("Scenery", "Shield Wall, Medium");
        db.Add("Scenery", "Shield Wall, Large");
        db.Add("Scenery", "Shield Wall, X-Large");

        // Hidden Structure Blocks
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");

        // Hidden Structure Blocks (2nd section - Spire-specific)
        db.Add("Structure", "Spire Cannon, Base");
        db.Add("Structure", "Spire Cannon, Cliff");
        db.Add("Structure", "Spire Cannon, Gun");

        return db;
    }

    private static ReachPaletteDatabase Boardwalk()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Dumpster");
        db.Add("Scenery", "Dumpster, Tall");
        db.Add("Scenery", "Plasma Storage");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        return db;
    }

    private static ReachPaletteDatabase Boneyard()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.Add("Vehicle", "Scorpion");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.Add("Equipment", "Golf Club");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.Add("Structure", "Ramp, Stunt");
        db.Add("Equipment", "Shield Door, Small");
        db.Add("Equipment", "Shield Door, Medium");
        db.Add("Equipment", "Shield Door, Large");
        db.Add("Equipment", "One Way Shield 1");
        db.Add("Equipment", "One Way Shield 2");
        db.Add("Equipment", "One Way Shield 3");
        db.Add("Equipment", "One Way Shield 4");
        db.Add("Equipment", "One Way Shield 5");
        db.Add("Equipment", "Shield Wall, Small");
        db.Add("Equipment", "Shield Wall, Medium");
        db.Add("Equipment", "Shield Wall, Large");
        db.Add("Equipment", "Shield Wall, X-Large");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        return db;
    }

    private static ReachPaletteDatabase Countdown()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Dumpster");
        db.Add("Scenery", "Dumpster, Tall");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Structure", "I-Beam");
        db.Add("Structure", "Cover, Large, Human");
        db.Add("Structure", "Door, Human");
        return db;
    }

    private static ReachPaletteDatabase Powerhouse()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Camping Stool, Covenant");
        db.Add("Scenery", "Folding Chair");
        db.Add("Scenery", "Covenant Antenna");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Dumpster");
        db.Add("Scenery", "Dumpster, Tall");
        db.Add("Scenery", "Plasma Storage");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        return db;
    }

    private static ReachPaletteDatabase Zealot()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool, Covenant");
        db.Add("Scenery", "Covenant Antenna");
        db.AddVariants("Scenery", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Plasma Storage");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Structure", "Platform, Covenant");
        return db;
    }

    private static ReachPaletteDatabase Anchor9()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Landmine");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.Add("Scenery", "Engine Cart");
        db.Add("Scenery", "Missile Cart");
        db.Add("Structure", "Wall");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Equipment", "Shield Door, Small", "Shield Door, Small 1", "Shield Door, Large", "Shield Door, Large 1");
        db.Add("Equipment", "Low Gravity Volume");
        db.Add("Equipment", "Shield Door, Large (Anchor 9)");
        db.Add("Equipment", "Shield Door, Small (Anchor 9)");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Objective", "Destination Delta");
        return db;
    }

    private static ReachPaletteDatabase Breakpoint()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.Add("Vehicle", "Banshee");
        db.Add("Vehicle", "Falcon");
        db.Add("Vehicle", "Ghost");
        db.Add("Vehicle", "Mongoose");
        db.AddVariants("Vehicle", "Warthog, Default", "Warthog, Gauss", "Warthog, Rocket");
        db.Add("Vehicle", "Wraith");
        db.Add("Vehicle", "Scorpion");
        db.AddVariants("Equipment", "Fusion Coil", "Landmine");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Covenant Barrier", "Portable Shield", "Heavy Barrier", "Jersey Barrier");
        db.AddVariants("Scenery", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open");
        db.Add("Scenery", "Street Cone");
        db.Add("Structure", "Bridge");
        db.AddVariants("Structure", "Door", "One Way Shield 1", "One Way Shield 2");
        db.Add("Structure", "Rock, Flat");
        db.Add("Structure", "Rock, Medium 1");
        db.Add("Structure", "Rock, Small");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Objective", "Destination Delta");
        db.Add("Objective", "Destination Zulu");
        return db;
    }

    private static ReachPaletteDatabase Condemned()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Camping Stool, Covenant");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Plasma Storage");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.AddVariants("Structure", "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved", "Wall, Coliseum", "Shield Door, Small", "Red (Condemned)", "Blue (Condemned)");
        return db;
    }

    private static ReachPaletteDatabase BattleCanyon()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift", "Gravity Lift, Forerunner", "Gravity Lift, Tall, Forerunner");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.AddVariants("Structure", "Block, 1x1", "Block, 1x1, Flat", "Block, 1x1, Short", "Block, 1x1, Tall", "Block, 1x1, Tall And Thin", "Block, 1x2", "Block, 1x4", "Block, 2x1, Flat", "Block, 2x2", "Block, 2x2, Flat", "Block, 2x2, Short", "Block, 2x2, Tall", "Block, 2x3", "Block, 2x4", "Block, 3x1, Flat");
        db.AddVariants("Structure", "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Corner, 45 Degrees", "Corner, 2x2");
        db.AddVariants("Structure", "Bunker, Small", "Bunker, Small, Covered");
        db.AddVariants("Structure", "Brace", "Column", "Cover", "Cover, Crenellation", "Railing, Small", "Railing, Medium", "Railing, Long", "Teleporter Frame");
        db.AddVariants("Structure", "Door, Forerunner 1", "Door, Forerunner 2");
        db.AddVariants("Structure", "Ramp, 1x2", "Ramp, 1x2, Shallow", "Ramp, 2x2", "Ramp, Bridge, Small");
        db.AddVariants("Structure", "Rock, Small", "Rock, Flat", "Rock, Medium 1", "Rock, Spire 2", "Rock Cluster, Blocker", "Rock, Large, Blocker");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Equipment", "Health Cabinet");
        return db;
    }

    private static ReachPaletteDatabase Penance()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift", "Gravity Lift, Covenant");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool, Covenant");
        db.Add("Scenery", "Covenant Antenna");
        db.AddVariants("Scenery", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.Add("Scenery", "Plasma Storage");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Structure", "Platform, Covenant");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Equipment", "Health Cabinet");
        return db;
    }

    private static ReachPaletteDatabase Solitary()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Metal, Multi", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open", "Crate, Forerunner, Small", "Crate, Forerunner, Large");
        db.Add("Scenery", "Dumpster");
        db.Add("Scenery", "Dumpster, Tall");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.AddVariants("Structure", "Block, 1x1", "Block, 1x1, Flat", "Block, 1x1, Short", "Block, 1x1, Tall", "Block, 1x1, Tall And Thin", "Block, 1x2", "Block, 1x4", "Block, 2x1, Flat", "Block, 2x2", "Block, 2x2, Flat", "Block, 2x2, Short", "Block, 2x2, Tall", "Block, 2x3", "Block, 2x4", "Block, 3x1, Flat", "Block, 3x3", "Block, 3x3, Flat", "Block, 3x3, Short", "Block, 3x4", "Block, 4x4", "Block, 4x4, Flat", "Block, 4x4, Short", "Block, 5x1, Short");
        db.AddVariants("Structure", "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Bridge, XLarge", "Bridge, Diagonal", "Bridge, Diag, Small", "Corner, 45 Degrees", "Corner, 2x2", "Corner, 4x4", "Platform, Y", "Catwalk, Straight", "Catwalk, Short", "Catwalk, Bend, Left", "Catwalk, Bend, Right");
        db.AddVariants("Structure", "Antenna, Small", "Brace", "Column", "Cover", "Cover, Crenellation", "Cover, Glass", "Glass Sail", "Railing, Small", "Railing, Medium", "Railing, Long", "Teleporter Frame", "Strut", "Cover, Small");
        db.AddVariants("Structure", "Door", "Door, Double", "Window", "Window, Double", "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved", "Tunnel, Short", "Tunnel, Long", "Door A, Forerunner", "Door B, Forerunner");
        db.AddVariants("Structure", "Bank, 1x1", "Bank, 1x2", "Bank, 2x1", "Bank, 2x2", "Ramp, 1x2", "Ramp, 1x2, Shallow", "Ramp, 2x2", "Ramp, 2x2, Steep", "Ramp, Circular, Small", "Ramp, Bridge, Small", "Ramp, Bridge, Medium", "Ramp, Bridge, Large");
        db.Add("Structure", "Grid");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Equipment", "Health Cabinet");
        db.AddVariants("Equipment", "Gravity Lift, Short (Solitary)", "Gravity Lift, Tall (Solitary)");
        return db;
    }

    private static ReachPaletteDatabase HighNoon()
    {
        var db = new ReachPaletteDatabase();
        db.AddUniversalEntries();
        db.AddVariants("Equipment", "Fusion Coil", "Landmine", "Plasma Battery", "Propane Tank");
        db.Add("Equipment", "Health Station");
        db.AddVariants("Equipment", "Camo Powerup", "Overshield", "Custom Powerup");
        db.AddVariants("Equipment", "Cannon, Man", "Cannon, Man, Heavy", "Cannon, Man, Light", "Cannon, Vehicle", "Gravity Lift", "Gravity Lift, Forerunner", "Gravity Lift, Tall, Forerunner");
        db.AddVariants("Equipment", "FX:Colorblind", "FX:Next Gen", "FX:Juicy", "FX:Nova", "FX:Olde Timey", "FX:Pen And Ink");
        db.AddVariants("Equipment", "Receiver Node", "Sender Node", "Two-Way Node");
        db.AddVariants("Equipment", "Die", "Golf Ball", "Golf Club", "Kill Ball", "Soccer Ball", "Tin Cup");
        db.AddVariants("Equipment", "Light, Red", "Light, Blue", "Light, Green", "Light, Orange", "Light, Purple", "Light, Yellow", "Light, White", "Light, Red, Flashing", "Light, Yellow, Flashing");
        db.Add("Spawner", "Initial Spawn");
        db.Add("Spawner", "Respawn Point");
        db.Add("Spawner", "Initial Loadout Camera");
        db.Add("Spawner", "Respawn Zone");
        db.Add("Spawner", "Respawn Zone, Weak");
        db.Add("Spawner", "Respawn Zone, Anti");
        db.AddVariants("Spawner", "Safe Boundary", "Soft Safe Boundary");
        db.AddVariants("Spawner", "Kill Boundary", "Soft Kill Boundary");
        db.Add("Objective", "Flag Stand");
        db.Add("Objective", "Capture Plate");
        db.Add("Objective", "Hill Marker");
        db.AddVariants("Scenery", "Barricade, Small", "Barricade, Large", "Jersey Barrier", "Jersey Barrier, Short", "Covenant Barrier", "Portable Shield");
        db.Add("Scenery", "Camping Stool");
        db.Add("Scenery", "Folding Chair");
        db.AddVariants("Scenery", "Crate, Small, Closed", "Crate, Metal, Multi", "Crate, Metal, Single", "Crate, Heavy Duty", "Crate, Heavy, Small", "Covenant Crate", "Crate, Half Open", "Crate, Fully Open");
        db.AddVariants("Scenery", "Sandbag Wall", "Sandbag, Turret Wall", "Sandbag Corner, 45", "Sandbag Corner, 90", "Sandbag Endcap");
        db.Add("Scenery", "Street Cone");
        db.AddVariants("Scenery", "Pallet", "Pallet, Large", "Pallet, Metal");
        db.AddVariants("Structure", "Block, 1x1", "Block, 1x1, Flat", "Block, 1x1, Short", "Block, 1x1, Tall", "Block, 1x1, Tall And Thin", "Block, 1x2", "Block, 1x4", "Block, 2x1, Flat", "Block, 2x2", "Block, 2x2, Flat", "Block, 2x2, Short", "Block, 2x2, Tall", "Block, 2x3", "Block, 2x4", "Block, 3x1, Flat", "Block, 3x3", "Block, 3x3, Flat", "Block, 3x3, Short", "Block, 3x3, Tall", "Block, Large", "Blocker, Hallway", "Door A, Forerunner", "Door B, Forerunner");
        db.AddVariants("Structure", "Bridge, Small", "Bridge, Medium", "Bridge, Large", "Bridge, XLarge", "Bridge, Diagonal", "Bridge, Diag, Small", "Corner, 45 Degrees", "Corner, 2x2", "Corner, 4x4", "Landing Pad", "Platform, Ramped", "Platform, Ramped, Stone", "Platform, Y", "Platform, Y, Large", "Sniper Nest", "Catwalk, Angled", "Catwalk, Large");
        db.AddVariants("Structure", "Bunker, Small", "Bunker, Small, Covered", "Bunker, Box");
        db.AddVariants("Structure", "Antenna, Small", "Antenna, Satellite", "Brace", "Brace, Large", "Brace, Tunnel", "Column", "Column, Stone", "Cover", "Cover, Crenellation", "Cover, Glass", "Railing, Small", "Railing, Medium", "Railing, Long", "Teleporter Frame", "Strut", "Walkway Cover, Short", "Tombstone", "Cover, Large, Stone");
        db.AddVariants("Structure", "Door", "Door, Double", "Window", "Window, Double", "Wall", "Wall, Double", "Wall, Corner", "Wall, Curved", "Tunnel, Short", "Tunnel, Long", "Wall, Small, Forerunner", "Wall, Large, Forerunner", "Door, Forerunner 1", "Door, Forerunner 2", "Cover, Large");
        db.AddVariants("Structure", "Bank, 1x1", "Bank, 1x2", "Bank, 2x1", "Bank, 2x2", "Ramp, 1x2", "Ramp, 1x2, Shallow", "Ramp, 2x2", "Ramp, 2x2, Steep", "Ramp, Bridge, Small", "Ramp, Bridge, Medium", "Ramp, Bridge, Large", "Ramp, Stunt");
        db.Add("Structure", "Grid");
        db.Add("Structure", "Block, 2x2, Invisible");
        db.Add("Structure", "Block, 1x1, Invisible");
        db.Add("Structure", "Block, 2x2x2, Invisible");
        db.Add("Structure", "Block, 4x4x2, Invisible");
        db.Add("Structure", "Block, 4x4x4, Invisible");
        db.Add("Structure", "Block, 2x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Flat, Invisible");
        db.Add("Structure", "Block, 1x1, Small, Invisible");
        db.Add("Structure", "Block, 2x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x2, Flat, Invisible");
        db.Add("Structure", "Block, 4x4, Flat, Invisible");
        db.Add("Equipment", "Health Cabinet");
        return db;
    }
}
