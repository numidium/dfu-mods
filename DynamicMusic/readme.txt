Dynamic Music
-------------------------
a Daggerfall Unity mod by Numidium3rd



---Table of Contents---
1. Description
2. Custom Tracks
3. User-Defined Playlists
	3a. Condition Syntax
	3b. Condition Lookup
	3c. Value Lookup
4. Credits



1. Description
-------------------------
This mod extends Daggerfall Unity's music system to fades between tracks, combat music, custom tracks for playlists,
and user-defined playlists. The goal is to eliminate the jarring changes between music tracks and to potentially lower
repetition by allowing greater customizability.

Dynamic Music will work in tandem with soundtrack replacers that simply replace the MIDI tracks with .ogg audio. If no
custom tracks are placed in a given DynMusic directory then either the original MIDIs or their .ogg replacements will
play for that playlist.

A playlist with an empty DynMusic playlist directory will choose vanilla tracks using vanilla logic.



2. Custom Tracks
-------------------------
Custom tracks are to be placed in any of the playlist directories within the StreamingAssets/Sound/DynMusic directory.
They must be in .ogg format in order to be recognized. Soundtracks with .ogg files will play their tracks in shuffle
order (i.e. all tracks play in random order before repeating).

If any .ogg files are added to a playlist during runtime then they will not become part of their respective playlists
until DFU restarts. Deleting .ogg files during runtime should be avoided as it will cause errors.



3. User-Defined Playlists
-------------------------
Users may define their own playlists separate from Daggerfall's vanilla soundtrack. The DynMusic directory contains a
file named UserDefined.txt which describes a list of custom music directories and the conditions under which they
play.

To define a custom playlist, first create a new directory in DynMusic and give it a unique name. Then add a line in
UserDefined.txt using the name of the new directory and a set of conditions under which the directory's playlist will
play. The proper syntax for this file is described in subsection 3a.

When testing a new playlist always check the Player.log for parser errors by searching "Dynamic Music". If any errors
are present in your condition then your playlist will not play.



3a. Condition Syntax
-------------------------
Each playlist condition line contains the directory name, a single-character separator, and a set of conditions,
repectively. In general terms:
PlaylistName = (Not) Condition0 (param0, ...paramN) ... (Not) ConditionN (param0, ... paramN) | Flag0, ...FlagN

Conditions, which are separated by commas, come in two types: Booleans and functions. Boolean conditions may be
written as-is or preceded with a "Not" for negation. Function conditions take one or more parameters and may NOT
be negated or else the line will be rejected and an error will be thrown. Passing multiple parameters to a
function condition will make it evaluate to true if it is true for any one of them.

If all specified conditions evaluate to true at any given time during runtime then Dynamic Music will use the
playlist.

Here is an example:
TemperateDaySunnyTown = Climate 300, Not Night, WeatherType 0, LocationType 0 1 2

The preceding set of conditions evaluate to true when the climate is temperate (code 300), the ingame time is during
the day (negated night), the sky is sunny (weather code 0), and the player's current location type is either a city,
hamlet, or village (code 0, 1, or 2). If all these conditions are met then the playlist will play.

Another example with flags included:
CombatDungeonBoss = Combat 5, Dungeon | CrashIn, ResumePrevious, PlayUntilCombatEnd

These conditions specify that this playlist should play if the player is encountering a hostile enemy that is at least
5 levels above their own and is inside a dungeon.


3b. Condition Lookup
-------------------------
i. Condition Functions:

    Night - True if time is between 18:00 and 6:00.
        Parameters: None
    Interior - True if player is in a building that isn't a dungeon or castle.
        Parameters: None
    Dungeon - True if player is in a dungeon.
        Parameters: None
    DungeonCastle - True if player is inside a castle.
        Parameters: None
    LocationType - True if player's outdoor location fits one of the parameters (see 3ci).
        Parameters: LocationType0 LocationType1 ... LocationTypeN
    BuildingType - True if player is inside one of the specified building types (see 3cii).
        Parameters: BuildingType0 BuildingType1 ... BuildingTypeN
    WeatherType - True if the current weather fits one of the given types (see 3ciii).
        Parameters: WeatherType0 WeatherType1 ... WeatherTypeN
    FactionId - True if the faction of the player's current environment fits one of the given values (see 3civ).
        Parameters: FactionId0 FactionId1 ... FactionIdN
    Climate - True if the climate base type fits one of the given values (see 3cv).
        Parameters: ClimateType0 ClimateType1 ... ClimateTypeN
    ClimateIndex - True if the climate subtype fits one of the given values. Supersedes Climate, which is kept for
    backwards compatibility. (see 3cvi)
        Parameters: ClimateIndex0 ClimateIndex1 ... ClimateIndexN
    RegionIndex - True if the player's location is within one of the given regions (see 3cvii).
        Parameters: RegionIndex0 RegionIndex1 ... RegionIndexN
    DungeonType - True if the player is inside a dungeon of one of the given types (see 3cviii).
        Parameters: DungeonType0 DungeonType1 ... DungeonTypeN
    BuildingQuality - True if the player is inside a building of one of the specified qualities (see 3cix).
        Parameters: QualityLevel0 QualityLevel1 ... QualityLevelN
    Season - True if the current in-game season matches the specified season code (see 3cx).
        Parameters: SeasonCode0 SeasonCode1 ... SeasonCodeN
    Month - True if the current in-game month matches the specified month code (see 3cxi).
        Parameters: Month0 Month1 ... MonthN
    StartMenu - True if the game is on the starting menu (the screen with new game, load game, and exit game).
        Parameters: None
    Combat - True if player is in combat.
        Parameters: MinimumLevelDifference (assumed to be 0 if not supplied)
    ReadingBook - True if the book reading window is open.
        Parameters: None
    Swimming - True if the player is in swimming state.
        Parameters: None
    BuildingIsOpen - True if the building occupied by the player is currently open (evaluates to true if the building does not have opening/closing hours).
        Parameters: None

ii: Condition Flags:

    CrashIn - Playlist should bypass Fade Out/Fade In sequence when conditions are met.
    ResumePrevious - When returning to the previous playlist, start at time offset when playlist last changed.
    PlayUntilCombatEnd - Do not change to another playlist until the player is out of combat.


3c. Value Lookup
-------------------------
i. LocationTypes:
	City = 0
	Hamlet = 1
	Village = 2
	Home (Farm) = 3
	Dungeon (Labyrinth) = 4
	Religion (Temple) = 5
	Tavern = 6
	Dungeon (Keep) = 7
	Home (Wealthy) = 8
	Religion (Cult) = 9
	Dungeon (Ruin) = 10
	Home (Poor) = 11
	Graveyard = 12
	Coven = 13
	Your Ship = 14
	
ii. BuildingTypes:
	Alchemist = 0
	HouseForSale = 1
	Armorer = 2
	Bank = 3
	Town4 = 4
	Bookseller = 5
	ClothingStore = 6
	FurnitureStore = 7
	GemStore = 8
	GeneralStore = 9
	Library = 10
	GuildHall = 11
	PawnShop = 12
	WeaponSmith = 13
	Temple = 14
	Tavern = 15
	Palace = 16
	House1 = 17 (Always has locked entry doors)
	House2 = 18 (Has unlocked doors from 0600-1800)
	House3 = 19
	House4 = 20
	House5 = 21
	House6 = 22
	Town23 = 23
	Ship = 24
	Special1 = 116
	Special2 = 223
	Special3 = 249
	Special4 = 250
	Any Shop = 65533
	Any House = 65534

iii. WeatherTypes:
	Sunny = 0
	Cloudy = 1
	Overcast = 2
	Fog = 3
	Rain = 4
	Thunder = 5
	Snow = 6

iv. FactionIds:
See https://github.com/Interkarma/daggerfall-unity/blob/master/Assets/Scripts/API/FactionFile.cs#L56
Use the numerical value under FactionIDs in conditions.

v. ClimateTypes:
	None = -1
	Desert = 0
	Mountain = 100
	Temperate = 300
	Swamp = 400
    
vi. ClimateIndices:
    Ocean = 223
    Desert = 224
    Desert2 (Dak'fron) = 225
    Mountain = 226
    Rainforest = 227
    Swamp = 228
    Subtropical = 229
    MountainWoods = 230
    Woodlands = 231
    HauntedWoodlands = 232
	
vii. RegionIndices:
See https://en.uesp.net/wiki/Daggerfall_Mod:Region_Numbers

viii. DungeonTypes:
	Crypt = 0
	Orc Stronghold = 1
	Human Stronghold = 2
	Prison = 3
	Desecrated Temple = 4
	Mine = 5
	Natural Cave = 6
	Coven = 7
	Vampire Haunt = 8
	Laboratory = 9
	Harpy Nest = 10
	Ruined Castle = 11
	Spider Nest = 12
	Giant Stronghold = 13
	Dragon's Den = 14
	Barbarian Stronghold = 15
	Volcanic Caves = 16
	Scorpion Nest = 17
	Cemetery = 18

ix. Quality Levels:
    "Rusty relics...": 1 -> 3
    "Sturdy shelves, cobbled together...": 4 -> 7
    "The shop is laid on in a practical manner...": 8 -> 13
    "The shop is better appointed than many...": 14 -> 17
    "Incense and soft music...": 18 -> 20 

x. Seasons:
    Summer = 0
    Winter = 1
    Rain = 2
Note: "Seasons" govern terrain texture placement. For actual seasons (winter, spring, summer, fall) use Month.

xi. Months:
    Morning Star = 1
    Sun's Dawn = 2,
    First Seed = 3,
    Rain's Hand = 4,
    Second Seed = 5,
    Midyear = 6,
    Sun's Height = 7,
    Last Seed = 8,
    Hearthfire = 9,
    Frostfall = 10,
    Sun's Dusk = 11,
    Evening Star = 12


4. Credits
-------------------------
Interkarma - for Daggerfall Unity and some code I re-purposed for this mod.
Everyone who left comments on Nexus Mods - For inspiration and feature ideas.
Lysandus' Tomb Discord - For being a place for modders to share their woes.
