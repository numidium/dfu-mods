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
    }
]

You may define as many objects within the "[]" array as needed but they must be separated with a comma. Don't place a comma after the final object,
however. Below is an example JSON file containing two replacements that you may copy & paste for use as a template.

[
	{
		"Regions": [01, 52],
		"FactionId": -1,
		"QualityMin": 1,
		"QualityMax": 20,
		"ChanceToReplace": 100,
		"TextureArchive": 182,
		"TextureRecord": 0,
		"FlatTextureName": "merchant",
		"FlatPortrait": 503
	},
	{
		"Regions": [01, 52],
		"FactionId": -1,
		"QualityMin": 1,
		"QualityMax": 20,
		"ChanceToReplace": 100,
		"TextureArchive": 183,
		"TextureRecord": 5,
		"FlatTextureName": "banker",
		"FlatPortrait": 504
	}
]


2a. JSON Object
-------------------------
Each individual JSON object defines a replacement. Objects begin with a "{", end with a "}", and are separated with a ",". Object property
definitions begin with the property name enclosed in quotes, are separated from their value by a ":", and are separated by other property definitions
by a ",". The following describes each property definition:

Regions - Integer Array - Region IDs that the replacement should appear in. The supplied values must be enclosed in square brackets ([]) and each value
must be separated by a ",". Supply a -1 in the first position to make the replacement apply to all regions. Subsequent values will be ignored.
See https://en.uesp.net/wiki/Daggerfall_Mod:Region_Numbers for region indices.

FactionId - Integer - A number representing the faction which the interior must belong to in order for the replacement to take place. Supply a -1 to
ignore the interior's faction.
See https://github.com/Interkarma/daggerfall-unity/blob/master/Assets/Scripts/API/FactionFile.cs#L56 for Faction IDs.
Use the numerical value under FactionIDs in conditions.

QualityMin/QualityMax - Integer - The minimum/maximum quality values of the interior for the replacement to take place. Use these to have your new
NPC flat only appear in interiors that are a certain degree of upscale or impoverished. The values range from 1 to 20 with 1 being lowest quality
and 20 being highest. To ignore this set QualityMin to 1 and QualityMax to 20.

ChanceToReplace - Integer - The percent chance that the replacement will take place given all other conditions are met. Ranges from 0 to 100. The
random roll is seeded by a persistent value so the result will always be the same for any given interior.

TextureArchive - Integer - The index of the archive file of the original flat graphic which will be replaced.

TextureRecord - Integer - The index of the record within the archive of the original flat graphic which will be replaced.

FlatTextureName - String - The name prefix of the replacement's flat graphic file(s). The files themselves should be named with the prefix followed
by an ascending numerical sequence starting from 0.

FlatPortrait - Integer - The index of the NPC's portrait used in the talk window. If greater than 502 then a custom portrait file must be supplied.
If set to -1 then the default portrait will be used.


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

Conflicts between replacements are reported to the log file. Search "FlatReplacer" in Player.log to see if any conflicts occurred.


4. Credits
-------------------------
Interkarma - for Daggerfall Unity and some code I re-purposed for this mod.
Magicono - for brainstorming feature ideas with me
