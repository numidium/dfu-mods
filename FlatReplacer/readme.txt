Flat Replacer
-------------------------
a Daggerfall Unity mod framework by Numidium3rd



---Table of Contents---
1. Description
2. Creating NPC Flat Replacements
    2a. JSON Object
    2b. Flat Images
    2c. Portrait Image
3. Troubleshooting
4. Credits



1. Description
-------------------------
This is a framework for replacing interior NPC flats with custom graphics. Vanilla DFU allows graphical replacements but only within vanilla parameters.
Flat Replacer allows the modder to specify particular situations when the usual NPC flats should be replaced allowing for more NPC flat diversity.
The goal of this framework is to allow multiple modders to make NPC flat replacements with minimal conflicts and processing overhead.


2. Creating NPC Flat Replacements
-------------------------
Each individual replacement is defined by 3 discrete components:
    1. A JSON object.
    2. A set of images for the flat (could be just one).
    3. A portrait image (optional).
    
The user must define the JSON object within a JSON file in the StreamingAssets/FlatReplacements directory. The names of the JSON files will not affect
operation of the mod. The important aspect is their syntax. A JSON file for this mod must be formatted as follows:

[
    {
        ...properties for first object defined here...
    },
    {
        ...properties for second object defined here...
    },
    ...and so on...
]

You may define as many objects within the "[]" array as needed but they must be separated with a comma. Don't place a comma after the final object,
however. Below is an example JSON file containing two replacements that you may copy & paste for use as a template.

[
	{
		"Regions": [01, 52],
		"FactionId": -1,
        "BuildingType": -1,
        "SocialGroup": 1,
		"QualityMin": 1,
		"QualityMax": 20,
		"TextureArchive": 182,
		"TextureRecord": 0,
        "ReplaceTextureArchive": -1,
        "ReplaceTextureRecord": -1,
		"FlatTextureName": "merchant",
        "UseExactDimensions": false,
		"FlatPortrait": 503
	},
	{
		"Regions": [01, 52],
		"FactionId": -1,
        "LocationTypes": 1,
        "BuildingType": -1,
		"QualityMin": 1,
		"QualityMax": 20,
        "NameBank": 6,
		"TextureArchive": 182,
		"TextureRecord": 0,
        "ReplaceTextureArchive": -1,
        "ReplaceTextureRecord": -1,
		"FlatTextureName": "banker",
        "UseExactDimensions": false,
		"FlatPortrait": 504
	}
]


2a. JSON Object
-------------------------
Each individual JSON object defines a replacement. Objects begin with a "{", end with a "}", and are separated with a ",". Object property
definitions begin with the property name enclosed in quotes, are separated from their value by a ":", and are separated by other property definitions
by a ",". Omitted values will be set to their defaults or wildcard values. The following describes each property definition:

Regions - Integer Array - Region IDs that the replacement should appear in. The supplied values must be enclosed in square brackets ([]) and each value
must be separated by a ",". Supply a -1 in the first position to make the replacement apply to all regions. Subsequent values will be ignored.
See https://en.uesp.net/wiki/Daggerfall_Mod:Region_Numbers for region indices.

LocationTypes - Integer - Represents the location type filter that replacement will be subjected to. No filter is applied by default. The following
values determine the filter:
0 - All location types (no filter)
1 - Buildings only
2 - Dungeons only
3 - Buildings and Castles (Currently the same as 0. This value will only be useful if more location types are added in a future release.)

FactionId - Integer - A number representing the faction which the interior must belong to in order for the replacement to take place. Supply a -1 to
ignore the interior's faction.
See https://github.com/Interkarma/daggerfall-unity/blob/master/Assets/Scripts/API/FactionFile.cs#L56 for Faction IDs.
Use the numerical value under FactionIDs in conditions.

BuildingType - Integer - The code of the town building which houses the static NPC. Use -1 for any building type.
See https://en.uesp.net/wiki/Daggerfall_Mod:Building_types for building type IDs. NOTE: You must supply these numbers in DECIMAL format. If you are
not familiar with hexidecimal->decimal conversion then use the "programmer" mode on Windows calculator and enter the hex values to see their decimal 
equivalents.

SocialGroup - Integer - The index of the social group the NPC belongs to. If the target NPC does not belong to the given social group then its flat
will not be replaced. The social group IDs are as follows:
0 = Commoners
1 = Merchants
2 = Scholars
3 = Nobility
4 = Underworld

QualityMin/QualityMax - Integer - The minimum/maximum quality values of the interior for the replacement to take place. Use these to have your new
NPC flat only appear in interiors that are a certain degree of upscale or impoverished. The values range from 1 to 20 with 1 being lowest quality
and 20 being highest. To ignore this set QualityMin to 1 and QualityMax to 20.

NameBank - Integer - The index of the name bank to substitute for the replaced NPC. If omitted (or an invalid index is chosen) then the current 
region's name bank will be used.
The name bank IDs are as follows:
Breton = 0
Redguard = 1
Nord = 2
DarkElf = 3
HighElf = 4
WoodElf = 5
Khajiit = 6
Argonian/Imperial = 7 (Argonians have Imperial names in Daggerfall.)
("Monster" names are excluded as they are not currently usable in a static NPC context.)

TextureArchive - Integer - The index of the archive file of the original flat graphic which will be replaced.

TextureRecord - Integer - The index of the record within the archive of the original flat graphic which will be replaced.

ReplaceTextureArchive/ReplaceTextureRecord - Integer - The vanilla texture archive/record to replace the current one in use with. If an invalid
record is specified then the object will be discarded. If a valid FlatTextureName is specified then these values will be ignored.

FlatTextureName - String - The name prefix of the replacement's custom flat graphic file(s). The files themselves should be named with the prefix
followed by an ascending numerical sequence starting from 0.

UseExactDimensions - Boolean - Whether or not to draw the image at its original size without any scaling. Unless you are using an image that was 
drawn in the low-res, pixelated style of the vanilla game you should probably leave this as false.

FlatPortrait - Integer - The index of the NPC's portrait used in the talk window. If greater than 502 then a custom portrait file must be supplied.
If set to -1 then the default portrait will be used.

Priority - Integer - The order of precedence a replacement should have independent of specificity. If one replacement's priority is higher than another then it will be chosen instead.
If more than one replacement has the same priority then one of them will be chosen at random. Defaults to 0.

2b. Flat Images
-------------------------
These should take the form of .png files placed in the StreamingAssets/Textures directory. The file names of each image should consist of a prefix
which matches the FlatTextureName value in its corresponding JSON object and 0-based index. For example, if you are creating an image with the prefix
"merchant" then name the file merchant0.png. If the flat is animated then name each subsequent frame "merchant1.png, merchant2.png..." and so on.


2c. Portrait Image
-------------------------
Custom portrait images, unfortunately, may not have custom prefixes. Each portrait must be in .png format and placed in StreamingAssets/Textures/CifRci.
The naming scheme for custom faces is either "TFAC00I0.RCI_(FlatPortrait #)-0.png" for common NPCs or "FACES.CIF_(FlatPortrait #)-0.png" for nobility.
If your JSON object uses an existing or the default portrait index then this section does not apply.


3. Troubleshooting
-------------------------
If your replacements aren't appearing then check the Player.log file in...
(DriveLetter):\Users\(UserName)\AppData\LocalLow\Daggerfall Workshop\Daggerfall Unity
...for errors. I don't know where that is on OSes that aren't Windows. Sorry.

Errors that occur when parsing or using replacement objects are reported to the log file. Search "FlatReplacer" in Player.log.


4. Credits
-------------------------
Interkarma - for Daggerfall Unity and some code I re-purposed for this mod.
Magicono - for brainstorming feature ideas with me
