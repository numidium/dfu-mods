using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace FutureShock
{
    sealed public class FutureShockWeapons : MonoBehaviour
    {
        private static Mod mod;
        private bool componentAdded;
        private static HitScanWeapon hitScanGun;
        private delegate void BsaTranslator(BinaryReader reader, ushort indexCount, Tuple<string, ushort>[] indexLookup, Dictionary<string, object> assetBank);
        private static DFPalette shockPalette;
        private const string gameDataPath = "F:\\dosgames\\futureshock\\doublepack\\Games\\The Terminator - Future Shock\\GAMEDATA\\";
        public static FutureShockWeapons Instance { get; private set; }
        public Type SaveDataType => typeof(FutureShockWeapons);
        public static string ModTitle => mod.Title;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<FutureShockWeapons>();
            //mod.SaveDataInterface = Instance;
            mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        void LoadSettings(ModSettings settings, ModSettingsChange change)
        {

        }

        private void Awake()
        {
            InitMod();
        }

        private void Update()
        {
            hitScanGun.IsFiring = InputManager.Instance.GetKey(KeyCode.Mouse1, false);
        }

        public static void InitMod()
        {
            //var settings = mod.GetSettings();

            // Import Textures
            var textureBank = new Dictionary<string, object>()
            {
                { "WEAPON01.CFA", null }, // Uzi
                { "WEAPON02.CFA", null }, // M16
            };

            shockPalette = new DFPalette($"{gameDataPath}SHOCK.COL");
            // Check for and/or load loose CFA files. Normally these will not exist until first run.
            var textureKeys = new List<string>(textureBank.Keys);
            foreach (var fileName in textureKeys)
                textureBank[fileName] = GetTextureAnimFromCfaFile($"{gameDataPath}{fileName}");
            var textureBsa = new FileProxy($"{gameDataPath}MDMDIMGS.BSA", FileUsage.UseMemory, true);
            var textureTranslator = new BsaTranslator(ReadTextureBsaData);
            ImportBsaAssets(textureBsa, textureBank, textureTranslator);
            textureBsa.Close();

            // Import Sounds
            var soundBank = new Dictionary<string, object>()
            {
                { "SHOTS2.RAW", null },
                { "SHOTS3.RAW", null },  // M16 shoot
                { "SHOTS5.RAW", null },  // Uzi shoot
                { "SHTGUN.RAW", null },  // Shotgun shoot
                { "SGCOCK1.RAW", null },
                { "SGCOCK2.RAW", null }
            };

            var soundBsa = new FileProxy($"{gameDataPath}MDMDSFXS.BSA", FileUsage.UseMemory, true);
            var soundTranslator = new BsaTranslator(ReadSoundBsaData);
            ImportBsaAssets(soundBsa, soundBank, soundTranslator);
            soundBsa.Close();
            var player = GameObject.FindGameObjectWithTag("Player");
            hitScanGun = player.AddComponent<HitScanWeapon>();
            hitScanGun.WeaponFrames = (Texture2D[])textureBank["WEAPON02.CFA"];
            hitScanGun.HorizontalOffset = 0.1f; // -0.3f for uzi
            hitScanGun.VerticalOffset = 0.01f;
            hitScanGun.ShootSound = (AudioClip)soundBank["SHOTS3.RAW"];
            Debug.Log("Future Shock Weapons initialized.");
        }

        private static void ImportBsaAssets(FileProxy bsaFile, Dictionary<string, object> assetBank, BsaTranslator bsaTranslator)
        {
            const ushort bsaIndexSize = 18; // name[12], u1, size, u2
            using (var reader = bsaFile.GetReader())
            {
                var indexCount = reader.ReadUInt16();
                var indexSize = (uint)(indexCount * bsaIndexSize);
                var indexOffset = (uint)(bsaFile.Buffer.Length - indexSize);
                // Read index
                reader.BaseStream.Seek(indexOffset, SeekOrigin.Begin);
                var indexLookup = new Tuple<string, ushort>[indexCount];
                var lookupIndex = 0;
                while (reader.BaseStream.Position < indexOffset + indexSize)
                {
                    var fileName = Encoding.UTF8.GetString(reader.ReadBytes(12)).Trim(new char[] { '\0' }); // File name
                    reader.ReadUInt16(); // Unused word
                    var size = reader.ReadUInt16(); // File size
                    reader.ReadUInt16(); // Unused word
                    indexLookup[lookupIndex++] = new Tuple<string, ushort>(fileName, size);
                }

                // Read data
                reader.BaseStream.Seek(sizeof(ushort), SeekOrigin.Begin); // Seek past initial index count
                bsaTranslator(reader, indexCount, indexLookup, assetBank);
            }
        }

        private static void ReadTextureBsaData(BinaryReader reader, ushort indexCount, Tuple<string, ushort>[] indexLookup, Dictionary<string, object> assetBank)
        {
            for (var textureIndex = 0; textureIndex < indexCount; textureIndex++)
            {
                var fileName = indexLookup[textureIndex].Item1;
                var fileLength = indexLookup[textureIndex].Item2;
                // Skip file if not in bank or already loaded.
                if (!assetBank.ContainsKey(fileName) || assetBank[fileName] != null)
                {
                    reader.BaseStream.Seek(fileLength, SeekOrigin.Current);
                    continue;
                }

                var textureData = reader.ReadBytes(fileLength);
                // Create a standalone CFA file that Interkarma's class can use.
                var cfaPath = $"{gameDataPath}{fileName}";
                using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(cfaPath, FileMode.Create)))
                {
                    binaryWriter.Write(textureData);
                }

                assetBank[fileName] = GetTextureAnimFromCfaFile(cfaPath);
            }
        }

        private static void ReadSoundBsaData(BinaryReader reader, ushort indexCount, Tuple<string, ushort>[] indexLookup, Dictionary<string, object> assetBank)
        {
            // Table ripped from Future Shock's memory during runtime
            byte[] noiseTable = { 0xDD, 0x83, 0x65, 0x57, 0xEA, 0x78, 0x08, 0x48, 0xB8, 0x01, 0x38, 0x94, 0x08, 0xDD, 0x3F, 0xC2, 0xBE, 0xAB, 0x76, 0xC6, 0x14 };
            for (var soundIndex = 0; soundIndex < indexCount; soundIndex++)
            {
                var fileName = indexLookup[soundIndex].Item1;
                var fileLength = indexLookup[soundIndex].Item2;
                // Skip file if not in bank.
                if (!assetBank.ContainsKey(fileName))
                {
                    reader.BaseStream.Seek(fileLength, SeekOrigin.Current);
                    continue;
                }

                var soundData = reader.ReadBytes(fileLength);
                // De-noisify the sound data using Future Shock's noise table.
                // Note: I believe that noisifying the sound files was intended as a data protection scheme.
                var noiseTableInd = 0;
                for (var i = 0; i < soundData.Length; i++)
                {
                    soundData[i] -= noiseTable[noiseTableInd];
                    noiseTableInd = (noiseTableInd + 1) % noiseTable.Length;
                }

                var samples = new float[soundData.Length];
                // Convert each sample byte to float in range -1 to 1.
                const float conversionFactor = 1.0f / 128.0f;
                for (var i = 0; i < soundData.Length; i++)
                    samples[i] = (soundData[i] - 128) * conversionFactor;
                var clip = AudioClip.Create(fileName, fileLength, 1, 11025, false);
                clip.SetData(samples, 0);
                assetBank[fileName] = clip;
            }
        }

        private static Texture2D[] GetTextureAnimFromCfaFile(string path)
        {
            var cfaFile = new CfaFile() { Palette = shockPalette };
            if (!cfaFile.Load(path, FileUsage.UseMemory, true))
                return null;
            var frameCount = cfaFile.GetFrameCount(0);
            var textureFrames = new Texture2D[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                var bitmap = cfaFile.GetDFBitmap(0, i);
                textureFrames[i] = new Texture2D(bitmap.Width, bitmap.Height)
                {
                    filterMode = FilterMode.Point
                };

                var colors = cfaFile.GetColor32(0, i, 0);
                textureFrames[i].SetPixels32(colors);
                textureFrames[i].Apply();
            }

            return textureFrames;
        }
    }
}
