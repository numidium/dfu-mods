using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wenzil.Console;

namespace DynamicMusic
{
    public sealed class DynamicMusic : MonoBehaviour
    {
        #region Playlists
        private enum MusicPlaylist : byte
        {
            DungeonInterior,
            Sunny,
            Cloudy,
            Overcast,
            Rain,
            Snow,
            Temple,
            Tavern,
            Night,
            Shop,
            MagesGuild,
            Interior,
            Palace,
            Castle,
            Court,
            //Sneaking,
            MainMenu,
            CharCreation,
            None
        }

        private enum MusicEnvironment  : byte
        {
            Castle,
            City,
            DungeonExterior,
            DungeonInterior,
            Graveyard,
            MagesGuild,
            Interior,
            Palace,
            Shop,
            Tavern,
            Temple,
            Wilderness
        }

        private enum State : byte
        {
            Normal,
            FadingOut,
            FadingIn
        }

        // Dungeon
        static SongFiles[] _dungeonSongs = new SongFiles[]
        {
            SongFiles.song_dungeon,
            SongFiles.song_dungeon5,
            SongFiles.song_dungeon6,
            SongFiles.song_dungeon7,
            SongFiles.song_dungeon8,
            SongFiles.song_dungeon9,
            SongFiles.song_gdngn10,
            SongFiles.song_gdngn11,
            SongFiles.song_gdungn4,
            SongFiles.song_gdungn9,
            SongFiles.song_04,
            SongFiles.song_05,
            SongFiles.song_07,
            SongFiles.song_15,
            SongFiles.song_28,
        };

        // Sunny
        static SongFiles[] _sunnySongs = new SongFiles[]
        {
            SongFiles.song_gday___d,
            SongFiles.song_swimming,
            SongFiles.song_gsunny2,
            SongFiles.song_sunnyday,
            SongFiles.song_02,
            SongFiles.song_03,
            SongFiles.song_22,
        };

        // Sunny FM Version
        static SongFiles[] _sunnySongsFM = new SongFiles[]
        {
            SongFiles.song_fday___d,
            SongFiles.song_fm_swim2,
            SongFiles.song_fm_sunny,
            SongFiles.song_02fm,
            SongFiles.song_03fm,
            SongFiles.song_22fm,
        };

        // Cloudy
        static SongFiles[] _cloudySongs = new SongFiles[]
        {
            SongFiles.song_gday___d,
            SongFiles.song_swimming,
            SongFiles.song_gsunny2,
            SongFiles.song_sunnyday,
            SongFiles.song_02,
            SongFiles.song_03,
            SongFiles.song_22,
            SongFiles.song_29,
            SongFiles.song_12,
        };

        // Cloudy FM
        static SongFiles[] _cloudySongsFM = new SongFiles[]
{
            SongFiles.song_fday___d,
            SongFiles.song_fm_swim2,
            SongFiles.song_fm_sunny,
            SongFiles.song_02fm,
            SongFiles.song_03fm,
            SongFiles.song_22fm,
            SongFiles.song_29fm,
            SongFiles.song_12fm,
};

        // Overcast/Fog
        static SongFiles[] _overcastSongs = new SongFiles[]
        {
            SongFiles.song_29,
            SongFiles.song_12,
            SongFiles.song_13,
            SongFiles.song_gpalac,
            SongFiles.song_overcast,
        };

        // Overcast/Fog FM Version
        static SongFiles[] _overcastSongsFM = new SongFiles[]
        {
            SongFiles.song_29fm,
            SongFiles.song_12fm,
            SongFiles.song_13fm,
            SongFiles.song_fpalac,
            SongFiles.song_fmover_c,
        };

        // Rain
        static SongFiles[] _rainSongs = new SongFiles[]
        {
            SongFiles.song_overlong,        // Long version of overcast
            SongFiles.song_raining,
            SongFiles.song_08,
        };

        // Snow
        static SongFiles[] _snowSongs = new SongFiles[]
        {
            SongFiles.song_20,
            SongFiles.song_gsnow__b,
            SongFiles.song_oversnow,
            SongFiles.song_snowing,         // Not used in classic
        };

        // Sneaking - Not used in classic
        static SongFiles[] _sneakingSongs = new SongFiles[]
        {
            SongFiles.song_gsneak2,
            SongFiles.song_sneaking,
            SongFiles.song_sneakng2,
            SongFiles.song_16,
            SongFiles.song_09,
            SongFiles.song_25,
            SongFiles.song_30,
        };

        // Temple
        static SongFiles[] _templeSongs = new SongFiles[]
        {
            SongFiles.song_ggood,
            SongFiles.song_gbad,
            SongFiles.song_ggood,
            SongFiles.song_gneut,
            SongFiles.song_gbad,
            SongFiles.song_ggood,
            SongFiles.song_gbad,
            SongFiles.song_gneut,
        };

        // Tavern
        static SongFiles[] _tavernSongs = new SongFiles[]
        {
            SongFiles.song_square_2,
            SongFiles.song_tavern,
            SongFiles.song_folk1,
            SongFiles.song_folk2,
            SongFiles.song_folk3,
        };

        // Night
        static SongFiles[] _nightSongs = new SongFiles[]
        {
            SongFiles.song_10,
            SongFiles.song_11,
            SongFiles.song_gcurse,
            SongFiles.song_geerie,
            SongFiles.song_gruins,
            SongFiles.song_18,
            SongFiles.song_21,          // For general midi song_10 is duplicated here in Daggerfall classic, although song_21fm is used in FM mode.
        };

        // Dungeon FM version
        static SongFiles[] _dungeonSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_dngn1,
            SongFiles.song_fm_dngn1,
            SongFiles.song_fm_dngn2,
            SongFiles.song_fm_dngn3,
            SongFiles.song_fm_dngn4,
            SongFiles.song_fm_dngn5,
            SongFiles.song_fdngn10,
            SongFiles.song_fdngn11,
            SongFiles.song_fdungn4,
            SongFiles.song_fdungn9,
            SongFiles.song_04fm,
            SongFiles.song_05fm,
            SongFiles.song_07fm,
            SongFiles.song_15fm,
            SongFiles.song_15fm,
        };

        // Day FM version
        static SongFiles[] _daySongsFM = new SongFiles[]
        {
            SongFiles.song_fday___d,
            SongFiles.song_fm_swim2,
            SongFiles.song_fm_sunny,
            SongFiles.song_02fm,
            SongFiles.song_03fm,
            SongFiles.song_22fm,
            SongFiles.song_29fm,
            SongFiles.song_12fm,
            SongFiles.song_13fm,
            SongFiles.song_fpalac,
        };

        // Weather - Raining FM version
        static SongFiles[] _weatherRainSongsFM = new SongFiles[]
        {
            SongFiles.song_fmover_c,
            SongFiles.song_fm_rain,
            SongFiles.song_08fm,
        };

        // Weather - Snowing FM version
        static SongFiles[] _weatherSnowSongsFM = new SongFiles[]
        {
            SongFiles.song_20fm,
            SongFiles.song_fsnow__b,
            SongFiles.song_fmover_s,
        };

        // Sneaking FM version
        static SongFiles[] _sneakingSongsFM = new SongFiles[]
        {
            SongFiles.song_fsneak2,
            SongFiles.song_fmsneak2,        // Used in Arena when trespassing in homes
            SongFiles.song_fsneak2,
            SongFiles.song_16fm,
            SongFiles.song_09fm,
            SongFiles.song_25fm,
            SongFiles.song_30fm,
        };

        // Temple FM version
        static SongFiles[] _templeSongsFM = new SongFiles[]
        {
            SongFiles.song_fgood,
            SongFiles.song_fbad,
            SongFiles.song_fgood,
            SongFiles.song_fneut,
            SongFiles.song_fbad,
            SongFiles.song_fgood,
            SongFiles.song_fbad,
            SongFiles.song_fneut,
        };

        // Tavern FM version
        static SongFiles[] _tavernSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_sqr_2,
        };

        // Night FM version
        static SongFiles[] _nightSongsFM = new SongFiles[]
        {
            SongFiles.song_11fm,
            SongFiles.song_fcurse,
            SongFiles.song_feerie,
            SongFiles.song_fruins,
            SongFiles.song_18fm,
            SongFiles.song_21fm,
        };

        // Unused dungeon music
        static SongFiles[] _unusedDungeonSongs = new SongFiles[]
        {
            SongFiles.song_d1,
            SongFiles.song_d2,
            SongFiles.song_d3,
            SongFiles.song_d4,
            SongFiles.song_d5,
            SongFiles.song_d6,
            SongFiles.song_d7,
            SongFiles.song_d8,
            SongFiles.song_d9,
            SongFiles.song_d10,
        };

        // Unused dungeon music FM version
        static SongFiles[] _unusedDungeonSongsFM = new SongFiles[]
        {
            SongFiles.song_d1fm,
            SongFiles.song_d2fm,
            SongFiles.song_d3fm,
            SongFiles.song_d4fm,
            SongFiles.song_d5fm,
            SongFiles.song_d6fm,
            SongFiles.song_d7fm,
            SongFiles.song_d8fm,
            SongFiles.song_d9fm,
            SongFiles.song_d10fm,
        };

        // Shop
        static SongFiles[] _shopSongs = new SongFiles[]
        {
            SongFiles.song_gshop,
        };

        // Shop FM version
        static SongFiles[] _shopSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_sqr_2,
        };

        // Mages Guild
        static SongFiles[] _magesGuildSongs = new SongFiles[]
        {
            SongFiles.song_gmage_3,
            SongFiles.song_magic_2,
        };

        // Mages Guild FM version
        static SongFiles[] _magesGuildSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_nite3,
        };

        // Interior
        static SongFiles[] _interiorSongs = new SongFiles[]
        {
            SongFiles.song_23,
        };

        // Interior FM version
        static SongFiles[] _interiorSongsFM = new SongFiles[]
        {
            SongFiles.song_23fm,
        };

        // Not used in classic. There is unused code to play it in knightly orders
        static SongFiles[] _unusedKnightSong = new SongFiles[]
        {
            SongFiles.song_17,
        };

        // FM version of above
        static SongFiles[] _unusedKnightSongFM = new SongFiles[]
        {
            SongFiles.song_17fm,
        };

        // Palace
        static SongFiles[] _palaceSongs = new SongFiles[]
        {
            SongFiles.song_06,
        };

        // Palace FM version
        static SongFiles[] _palaceSongsFM = new SongFiles[]
        {
            SongFiles.song_06fm,
        };

        // Castle
        static SongFiles[] _castleSongs = new SongFiles[]
        {
            SongFiles.song_gpalac,
        };

        // Castle FM Version
        static SongFiles[] _castleSongsFM = new SongFiles[]
        {
            SongFiles.song_fpalac,
        };

        // Court
        static SongFiles[] _courtSongs = new SongFiles[]
        {
            SongFiles.song_11,
        };

        // Court FM Version
        static SongFiles[] _courtSongsFM = new SongFiles[]
        {
            SongFiles.song_11fm,
        };

        public SongFiles[] DungeonInteriorSongs = _dungeonSongs;
        public SongFiles[] SunnySongs = _sunnySongs;
        public SongFiles[] CloudySongs = _cloudySongs;
        public SongFiles[] OvercastSongs = _overcastSongs;
        public SongFiles[] RainSongs = _rainSongs;
        public SongFiles[] SnowSongs = _snowSongs;
        public SongFiles[] TempleSongs = _templeSongs;
        public SongFiles[] TavernSongs = _tavernSongs;
        public SongFiles[] NightSongs = _nightSongs;
        public SongFiles[] ShopSongs = _shopSongs;
        public SongFiles[] MagesGuildSongs = _magesGuildSongs;
        public SongFiles[] InteriorSongs = _interiorSongs;
        public SongFiles[] PalaceSongs = _palaceSongs;
        public SongFiles[] CastleSongs = _castleSongs;
        public SongFiles[] CourtSongs = _courtSongs;
        public SongFiles[] SneakingSongs = _sneakingSongs;
        #endregion Playlists

        /// <summary>
        /// Holds a list of strings that represent music tracks. Advances track and auto shuffles.
        /// </summary>
        private sealed class Playlist
        {
            public enum Flags : byte
            {
                None = 0x00,
                CrashIn = 0x01,
                ResumePrevious = 0x02,
                PlayUntilCombatEnd = 0x04
            }

            private readonly string[] tracks;
            private int index = 0;
            private void ShuffleTracks()
            {
                if (tracks.Length < 3)
                    return;
                var endTrack = tracks[tracks.Length - 1];
                // Fisher-Yates shuffle
                for (var i = tracks.Length - 1; i > 0; i--)
                {
                    var randIndex = UnityEngine.Random.Range(0, i + 1);
                    (tracks[randIndex], tracks[i]) = (tracks[i], tracks[randIndex]);
                }

                // Ensure that 0 doesn't swap with n-1. Otherwise there will be a repeat on re-shuffle.
                if (tracks[0] == endTrack)
                    (tracks[0], tracks[tracks.Length - 1]) = (tracks[tracks.Length - 1], tracks[0]);
            }

            public Playlist(List<string> trackList)
            {
                tracks = trackList.ToArray();
                ShuffleTracks();
            }

            public string GetNextTrack()
            {
                index = (index + 1) % tracks.Length;
                if (index == 0)
                    ShuffleTracks();
                return tracks[index];
            }

            public static Flags GetFlagsFromText(string text)
            {
                var flagText = text.ToLower();
                var flagValues = (Flags[])Enum.GetValues(typeof(Flags));
                for (var i = 0; i < flagValues.Length; i++)
                {
                    if (flagValues[i].ToString().ToLower() == flagText)
                        return flagValues[i];
                }

                return Flags.None;
            }

            public int TrackCount => tracks.Length;
            public string CurrentTrack => tracks[index];
            public Flags PlaylistFlags;
        }

        private sealed class ConditionUsage
        {
            public enum Conditions : UInt16
            {
                None,
                Night,
                Interior,
                Dungeon,
                DungeonCastle,
                LocationType,
                BuildingType,
                WeatherType,
                FactionId,
                Climate,
                ClimateIndex,
                RegionIndex,
                DungeonType,
                BuildingQuality,
                Season,
                Month,
                StartMenu,
                ReadingBook,
                Combat,
                Swimming,
                BuildingIsOpen
            }

            public static Conditions GetConditionFromText(string text)
            {
                var conditionText = text.ToLower();
                var conditionValues = (Conditions[])Enum.GetValues(typeof(Conditions));
                for (var i = 0; i < conditionValues.Length; i++)
                {
                    if (conditionValues[i].ToString().ToLower() == conditionText)
                        return conditionValues[i];
                }

                return Conditions.None;
            }

            public bool NegateArg { get; set; }
            public int[] ParameterArgs { get; set; }
            public Conditions Condition { get; set; }
        }

        public static DynamicMusic Instance { get; private set; }
        private static Mod mod;
        private DaggerfallUnity daggerfallUnity;
        private PlayerGPS localPlayerGPS;
        private PlayerEnterExit playerEnterExit;
        private PlayerWeather playerWeather;
        private DynamicSongPlayer dynamicSongPlayer;
        private GameManager gameManager;
        private PlayerEntity playerEntity;
        private const float detectionCheckInterval = 3f;
        private float detectionCheckDelta;
        private const float fadeOutLength = 2f;
        private const float fadeInLength = 2f;
        private float fadeOutTime;
        private float fadeInTime;
        private const byte combatTaperLength = 2;
        private byte combatTaper;
        private Playlist[] customPlaylists;
        private Dictionary<int, ConditionUsage[]> userDefinedConditionSets;
        private string currentCustomTrack;
        private bool customTrackQueued;
        private bool combatMusicIsEnabled;
        private bool isInCombat;
        private int maxEnemyLevel;
        private bool resumeIsEnabled = true;
        private bool loopCustomTracks;
        private float previousTimeSinceStartup;
        private float deltaTime;
        private float resumeSeeker = 0f;
        private int resumePlaylist;
        private bool gameLoaded;
        private int currentPlaylist;
        private bool randomIndRequested;
        private State currentState;
        private string debugPlaylistName;
        private string debugSongName;
        private GUIStyle guiStyle;
        private const string fileSearchPattern = "*.ogg";
        private const string modSignature = "Dynamic Music";

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<DynamicMusic>();
            Instance.dynamicSongPlayer = go.AddComponent<DynamicSongPlayer>();
            Instance.dynamicSongPlayer.ModSignature = modSignature;
            DontDestroyOnLoad(go);
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            StartGameBehaviour.OnStartGame += StartGameBehaviour_OnStartGame;
            mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        // Load settings that can change during runtime.
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            combatMusicIsEnabled = settings.GetValue<bool>("Options", "Enable Combat Music");
            resumeIsEnabled = settings.GetValue<bool>("Options", "Enable Track Resume");
            loopCustomTracks = settings.GetValue<bool>("Options", "Loop Custom Tracks");
        }

        private void Start()
        {   
            daggerfallUnity = DaggerfallUnity.Instance;
            // Load settings that require a restart.
            var settings = mod.GetSettings();
            LoadSettings(settings, new ModSettingsChange());

            const string soundDirectory = "Sound";
            const string baseDirectory = "DynMusic";
            var basePath = Path.Combine(Application.streamingAssetsPath, soundDirectory, baseDirectory);
            var userDefinedPlaylists = new List<Playlist>();

            // Load user-defined playlists from disk (user-defined meaning the conditions and tracks are custom).
            const string userPlaylistsFileName = "UserDefined.txt";
            var filePath = Path.Combine(basePath, userPlaylistsFileName);
            if (Directory.Exists(basePath) && File.Exists(filePath))
            {
                using (var file = new StreamReader(filePath))
                {
                    userDefinedConditionSets = new Dictionary<int, ConditionUsage[]>();
                    ushort lineCounter = 0;
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        line = line.Trim(); // Remove whitespace to the left and right.
                        lineCounter++;
                        if (line == string.Empty || line[0] == '#') // # = Comment/memo line, ignore.
                            continue;
                        // Get track names from directory.
                        List<string> trackList;
                        var lineContainsError = false;
                        string[] flagTokens = null;
                        const char flagSeparator = '|';
                        if (line.Split(flagSeparator).Length > 1)
                            flagTokens = line.Split(flagSeparator)[1].Split(' ', ',');
                        var conditionTokens = line.Split(flagSeparator)[0].Split(' ', ',');
                        var playlistName = conditionTokens[0];
                        if (!Directory.Exists(Path.Combine(basePath, playlistName)))
                        {
                            PrintParserError($"Reference to non-existent playlist directory", lineCounter, playlistName);
                            continue; // Throw out this line.
                        }
                        else
                        {
                            var files = Directory.GetFiles(Path.Combine(basePath, playlistName), fileSearchPattern);
                            trackList = new List<string>();
                            if (files.Length > 0)
                            {
                                foreach (var fileName in files)
                                    trackList.Add(fileName);
                                // Add playlist.
                                userDefinedPlaylists.Add(new Playlist(trackList));
                            }
                        }

                        // Parse conditional tokens on current line.
                        var playlistKey = (int)MusicPlaylist.None + userDefinedPlaylists.Count;
                        var conditionSet = new List<ConditionUsage>();
                        var tokenIndex = 2;
                        while (tokenIndex < conditionTokens.Length)
                        {
                            var negate = false;
                            if (conditionTokens[tokenIndex].ToLower() == "not")
                            {
                                tokenIndex++;
                                negate = true;
                            }

                            var condition = ConditionUsage.GetConditionFromText(conditionTokens[tokenIndex]);
                            if (condition == ConditionUsage.Conditions.None)
                            {
                                PrintParserError($"Unrecognized condition", lineCounter, conditionTokens[tokenIndex]);
                                lineContainsError = true;
                                break;
                            }

                            tokenIndex++;
                            if (!lineContainsError)
                            {
                                var arguments = new List<int>();
                                while (tokenIndex < conditionTokens.Length && conditionTokens[tokenIndex] != "")
                                {
                                    if (!int.TryParse(conditionTokens[tokenIndex++], out var result))
                                    {
                                        PrintParserError($"Invalid argument", lineCounter, condition.ToString());
                                        lineContainsError = true;
                                        break;
                                    }

                                    arguments.Add(result);
                                }

                                var conditionUsage = new ConditionUsage()
                                {
                                    NegateArg = negate,
                                    ParameterArgs = arguments.ToArray(),
                                    Condition = condition
                                };

                                conditionSet.Add(conditionUsage);
                            }

                            tokenIndex++;
                        }

                        tokenIndex = 0;
                        if (flagTokens != null)
                        {
                            while (tokenIndex < flagTokens.Length)
                                userDefinedPlaylists[userDefinedPlaylists.Count - 1].PlaylistFlags |= Playlist.GetFlagsFromText(flagTokens[tokenIndex++]);
                        }

                        if (lineContainsError)
                            continue; // Don't tolerate errors.
                        userDefinedConditionSets[playlistKey] = conditionSet.ToArray();
                    }
                }
            }

            // Load custom playlists from disk.
            customPlaylists = new Playlist[(int)MusicPlaylist.None + userDefinedPlaylists.Count + 1];
            for (var i = 0; i < (int)MusicPlaylist.None + 1; i++)
            {
                var playlistPath = Path.Combine(basePath, $"{(MusicPlaylist)i}");
                if (!Directory.Exists(playlistPath))
                {
                    Directory.CreateDirectory(playlistPath);
                    continue;
                }

                var files = Directory.GetFiles(playlistPath, fileSearchPattern);
                if (files.Length == 0)
                    continue;
                var trackList = new List<string>();
                foreach (var fileName in files)
                    trackList.Add(fileName);
                customPlaylists[i] = new Playlist(trackList);
            }

            // Append user defined playlists to custom playlists.
            var j = 0;
            for (var i = (int)MusicPlaylist.None + 1; j < userDefinedPlaylists.Count; i++)
                customPlaylists[i] = userDefinedPlaylists[j++];

            // Remove vanilla song players from scene.
            const string songPlayerName = "SongPlayer";
            gameManager = GameManager.Instance;
            Destroy(gameManager.DungeonParent.transform.Find(songPlayerName).gameObject);
            Destroy(gameManager.InteriorParent.transform.Find(songPlayerName).gameObject);
            Destroy(gameManager.ExteriorParent.transform.Find(songPlayerName).gameObject);
            // Replace with dummy objects of same name (for compatibility).
            var dummySongPlayer = new GameObject(songPlayerName);
            dummySongPlayer.transform.parent = gameManager.DungeonParent.transform;
            dummySongPlayer = new GameObject(songPlayerName);
            dummySongPlayer.transform.parent = gameManager.InteriorParent.transform;
            dummySongPlayer = new GameObject(songPlayerName);
            dummySongPlayer.transform.parent = gameManager.ExteriorParent.transform;

            // Set references for quick access.
            playerEntity = gameManager.PlayerEntity;
            localPlayerGPS = gameManager.PlayerGPS;
            playerEnterExit = localPlayerGPS.GetComponent<PlayerEnterExit>();
            playerWeather = localPlayerGPS.GetComponent<PlayerWeather>();

            // Set timing variables and state.
            previousTimeSinceStartup = Time.realtimeSinceStartup;
            gameLoaded = false;
            currentState = State.FadingIn;
            fadeInTime = fadeInLength;
            currentPlaylist = (int)MusicPlaylist.None;

            // Use alternate music if set.
            if (DaggerfallUnity.Settings.AlternateMusic)
            {
                DungeonInteriorSongs = _dungeonSongsFM;
                SunnySongs = _sunnySongsFM;
                CloudySongs = _cloudySongsFM;
                OvercastSongs = _overcastSongsFM;
                RainSongs = _weatherRainSongsFM;
                SnowSongs = _weatherSnowSongsFM;
                TempleSongs = _templeSongsFM;
                TavernSongs = _tavernSongsFM;
                NightSongs = _nightSongsFM;
                ShopSongs = _shopSongsFM;
                MagesGuildSongs = _magesGuildSongsFM;
                InteriorSongs = _interiorSongsFM;
                PalaceSongs = _palaceSongsFM;
                CastleSongs = _castleSongsFM;
                CourtSongs = _courtSongsFM;
                SneakingSongs = _sneakingSongsFM;
            }

            // Setup events.
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            playerEntity.OnDeath += OnDeath;
            guiStyle = new GUIStyle();
            guiStyle.normal.textColor = Color.black;
            Debug.Log($"{modSignature} initialized.");
            mod.IsReady = true;
        }

        private void Update()
        {
            // Use custom delta time so that time continues adding on pause.
            var realTimeSinceStartup = Time.realtimeSinceStartup;
            deltaTime = realTimeSinceStartup - previousTimeSinceStartup;
            previousTimeSinceStartup = realTimeSinceStartup;
            if (deltaTime < 0f)
                deltaTime = 0f;
            var previousPlaylist = currentPlaylist;
            if (!isInCombat || customPlaylists[previousPlaylist] == null || (customPlaylists[previousPlaylist].PlaylistFlags & Playlist.Flags.PlayUntilCombatEnd) == 0)
            {
                currentPlaylist = (int)GetMusicPlaylist(localPlayerGPS, playerEnterExit, playerWeather);

                // Check if conditions match any user-defined condition sets.
                if (currentPlaylist != (int)MusicPlaylist.None)
                {
                    var key = GetUserDefinedPlaylistKey(userDefinedConditionSets);
                    if (key >= 0)
                        currentPlaylist = key;
                }
            }

            switch (currentState)
            {
                case State.Normal:
                    if (currentPlaylist != previousPlaylist && previousPlaylist != (int)MusicPlaylist.None)
                    {
                        if (customPlaylists[currentPlaylist] != null && (customPlaylists[currentPlaylist].PlaylistFlags & Playlist.Flags.CrashIn) == Playlist.Flags.CrashIn)
                        {
                            currentState = State.FadingOut;
                            fadeOutTime = fadeOutLength;
                            fadeInTime = fadeInLength;
                            if ((customPlaylists[currentPlaylist].PlaylistFlags & Playlist.Flags.ResumePrevious) == Playlist.Flags.ResumePrevious)
                                resumePlaylist = previousPlaylist;
                        }
                        else
                            currentState = State.FadingOut;
                    }

                    if (currentState != State.FadingOut)
                        PlayCurrentTrack();

                    // Stop music if no playlist found.
                    if (currentPlaylist == (int)MusicPlaylist.None)
                    {
                        if (dynamicSongPlayer.IsPlaying)
                        {
                            currentCustomTrack = string.Empty;
                            dynamicSongPlayer.Stop();
                        }

                        break;
                    }

                    dynamicSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                    break;
                case State.FadingOut:
                    fadeOutTime += deltaTime;
                    dynamicSongPlayer.AudioSource.volume = Mathf.Lerp(DaggerfallUnity.Settings.MusicVolume, 0f, fadeOutTime / fadeOutLength);
                    if (fadeOutTime >= fadeOutLength)
                    {
                        // End fade when time elapsed.
                        fadeOutTime = 0f;
                        currentState = State.FadingIn;
                        randomIndRequested = true;
                    }

                    break;
                case State.FadingIn:
                    if (currentPlaylist != previousPlaylist && previousPlaylist != (int)MusicPlaylist.None)
                    {
                        if (customPlaylists[currentPlaylist] != null && (customPlaylists[currentPlaylist].PlaylistFlags & Playlist.Flags.CrashIn) == Playlist.Flags.CrashIn)
                        {
                            currentState = State.FadingOut;
                            fadeOutTime = fadeOutLength;
                            fadeInTime = fadeInLength;
                            break;
                        }
                        else
                        {
                            currentState = State.FadingOut;
                            fadeInTime = 0f;
                            break;
                        }
                    }

                    if (currentPlaylist == (int)MusicPlaylist.None)
                        break;
                    if (dynamicSongPlayer.AudioSource.volume == 0f)
                        PlayCurrentTrack();
                    fadeInTime += deltaTime;
                    // End fade when time elapsed.
                    if (fadeInTime >= fadeInLength)
                    {
                        fadeInTime = 0f;
                        currentState = State.Normal;
                        // Syncing the volume here instead of waiting for the next frame prevents audio hiccup.
                        dynamicSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                    }
                    else
                        dynamicSongPlayer.AudioSource.volume = Mathf.Lerp(0f, DaggerfallUnity.Settings.MusicVolume, fadeInTime / fadeInLength);
                    break;
            }

            // Check if player is in combat at every interval.
            detectionCheckDelta += Time.deltaTime;
            if (detectionCheckDelta < detectionCheckInterval)
                return;
            if (currentState == State.Normal)
            {
                if (combatMusicIsEnabled && !gameManager.PlayerDeath.DeathInProgress && !playerEntity.Arrested && GetCombatStatus(out maxEnemyLevel))
                {
                    isInCombat = true;
                    combatTaper = combatTaperLength;
                }
                else if (combatTaper == 0 || --combatTaper <= 0)
                    isInCombat = false;
            }

            detectionCheckDelta = 0f;
        }

        private void OnGUI()
        {
            if (Event.current.type.Equals(EventType.Repaint) && DefaultCommands.showDebugStrings)
            {
                var playing = dynamicSongPlayer.IsPlaying ? "Playing" : "Stopped";
                var text = $"Dynamic Music - {playing} - State: {currentState} - Playlist: {debugPlaylistName} - Song: {debugSongName}";
                GUI.Label(new Rect(10, 50, 800, 24), text, guiStyle);
                GUI.Label(new Rect(8, 48, 800, 24), text);
            }
        }

        private bool GetIsConditionTrue(ConditionUsage.Conditions condition, bool negate, int[] parameters)
        {
            var conditionResult = false;
            // Vanilla conditions:
            if (condition == ConditionUsage.Conditions.Night)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && daggerfallUnity.WorldTime.Now.IsNight;
            }
            else if (condition == ConditionUsage.Conditions.Interior)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && playerEnterExit.IsPlayerInside;
            }
            else if (condition == ConditionUsage.Conditions.Dungeon)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && playerEnterExit.IsPlayerInsideDungeon;
            }
            else if (condition == ConditionUsage.Conditions.DungeonCastle)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && playerEnterExit.IsPlayerInsideDungeonCastle;
            }
            else if (condition == ConditionUsage.Conditions.LocationType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start || playerEnterExit.IsPlayerInsideDungeon || !localPlayerGPS.IsPlayerInLocationRect) return false;
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.CurrentLocationType == (DFRegion.LocationTypes)parameter;
            }
            else if (condition == ConditionUsage.Conditions.BuildingType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerEnterExit.BuildingType == (DFLocation.BuildingTypes)parameter;
            }
            else if (condition == ConditionUsage.Conditions.WeatherType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerWeather.WeatherType == (DaggerfallWorkshop.Game.Weather.WeatherType)parameter;
            }
            else if (condition == ConditionUsage.Conditions.FactionId)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerEnterExit.FactionID == parameter;
            }
            // Non-vanilla conditions:
            else if (condition == ConditionUsage.Conditions.Climate)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                // TODO: make sure parameter is valid type and catch if not
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.ClimateSettings.ClimateType == (DFLocation.ClimateBaseType)parameter;
            }
            else if (condition == ConditionUsage.Conditions.ClimateIndex)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.CurrentClimateIndex == parameter;
            }
            else if (condition == ConditionUsage.Conditions.RegionIndex)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.CurrentRegionIndex == parameter;
            }
            else if (condition == ConditionUsage.Conditions.DungeonType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= (playerEnterExit.Dungeon != null) && playerEnterExit.Dungeon.Summary.DungeonType == (DFRegion.DungeonTypes)parameter;
            }
            else if (condition == ConditionUsage.Conditions.BuildingQuality)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerEnterExit.BuildingDiscoveryData.quality == parameter;
            }
            else if (condition == ConditionUsage.Conditions.Season)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= gameManager.StreamingWorld.CurrentPlayerLocationObject.CurrentSeason == (ClimateSeason)parameter;
            }
            else if (condition == ConditionUsage.Conditions.Month)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.MonthOfYear == parameter;
            }
            else if (condition == ConditionUsage.Conditions.StartMenu)
            {
                conditionResult = gameManager.StateManager.CurrentState == StateManager.StateTypes.Start;
            }
            else if (condition == ConditionUsage.Conditions.Combat)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                conditionResult = isInCombat;
                if (parameters.Length > 0)
                    conditionResult &= maxEnemyLevel - playerEntity.Level >= parameters[0];
            }
            else if (condition == ConditionUsage.Conditions.Swimming)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                conditionResult = playerEnterExit.IsPlayerSwimming;
            }
            else if (condition == ConditionUsage.Conditions.BuildingIsOpen)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                if (playerEnterExit.Interior != null && (int)playerEnterExit.Interior.BuildingData.BuildingType >= PlayerActivate.openHours.Length)
                    conditionResult = true;
                else
                    conditionResult = playerEnterExit.IsPlayerInside && !playerEnterExit.IsPlayerInsideDungeon
                        && PlayerActivate.IsBuildingOpen(playerEnterExit.Interior.BuildingData.BuildingType);
            }

            return negate ? !conditionResult : conditionResult;
        }

        private int GetUserDefinedPlaylistKey(Dictionary<int, ConditionUsage[]> conditionSets)
        {
            foreach (var key in conditionSets.Keys)
            {
                var eval = true;
                foreach (var condition in conditionSets[key])
                {
                    eval &= GetIsConditionTrue(condition.Condition, condition.NegateArg, condition.ParameterArgs);
                    if (!eval)
                        break;
                }

                if (eval)
                {
                    return key;
                }
            }

            return -1;
        }

        private void PrintParserError(string text, ushort lineNumber, string token)
        {
            Debug.Log($"{modSignature} user-defined playlist: {text} at line {lineNumber}: {token}");
        }

        private void HandleLocationChange()
        {
            combatTaper = 0;
        }

        private void PlayCurrentTrack()
        {
            var currentCustomPlaylist = customPlaylists[currentPlaylist] != null ? currentPlaylist : (int)MusicPlaylist.None;
            var isUsingCustomPlaylist = currentCustomPlaylist != (int)MusicPlaylist.None;
            // Plays random tracks continuously as long as custom tracks are available for the current context.
            // Plays one random custom track on loop if custom track looping is enabled.
            if (isUsingCustomPlaylist &&
                (currentCustomTrack != customPlaylists[currentPlaylist].CurrentTrack || customTrackQueued)) // Changed to a different custom track.
            {
                var playlist = customPlaylists[currentCustomPlaylist];
                var track = resumeSeeker > 0f || (loopCustomTracks && currentCustomTrack == customPlaylists[currentPlaylist].CurrentTrack) ? playlist.CurrentTrack : playlist.GetNextTrack();
                GetDebuggingText(track, out debugPlaylistName, out debugSongName, currentPlaylist > (int)MusicPlaylist.None);
                if (resumeIsEnabled && (customPlaylists[currentPlaylist].PlaylistFlags & Playlist.Flags.ResumePrevious) == Playlist.Flags.ResumePrevious)
                {
                    resumeSeeker = dynamicSongPlayer.CurrentSecond;
                    dynamicSongPlayer.Play(track, 0f);
                }
                else
                {
                    if (currentCustomPlaylist != resumePlaylist)
                        resumeSeeker = 0f;
                    dynamicSongPlayer.Play(track, resumeSeeker);
                    resumeSeeker = 0f;
                }

                currentCustomTrack = playlist.CurrentTrack;
                dynamicSongPlayer.Song = SongFiles.song_none;
                customTrackQueued = false;
            }
            // Loop the music as usual if no custom soundtracks are found.
            else if (currentCustomPlaylist == (int)MusicPlaylist.None)
            {
                var song = GetSong((MusicPlaylist)currentPlaylist);
                if (song == dynamicSongPlayer.Song)
                    return;
                GetDebuggingText(song, out debugPlaylistName, out debugSongName);
                if (currentPlaylist != resumePlaylist)
                    resumeSeeker = 0f;
                dynamicSongPlayer.Play(song, resumeSeeker);
                resumeSeeker = 0f;
                currentCustomTrack = string.Empty; // Should not be a custom track set if one is not playing.
            }

            // Queue next custom track when at the end of the current one.
            if (isUsingCustomPlaylist && dynamicSongPlayer.IsStoppedClip)
                customTrackQueued = true;
        }

        private MusicPlaylist GetMusicPlaylist(PlayerGPS localPlayerGPS, PlayerEnterExit playerEnterExit, PlayerWeather playerWeather)
        {
            // Note: Code was adapted from SongManager.cs. Credit goes to Interkarma for the original implementation.
            var dfUnity = DaggerfallUnity.Instance;
            var topWindow = DaggerfallUI.UIManager.TopWindow;
            if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start && !(topWindow is DaggerfallVidPlayerWindow) && !(topWindow is DaggerfallHUD))
            {
                if (topWindow is DaggerfallStartWindow ||
                            topWindow is DaggerfallUnitySaveGameWindow ||
                            (topWindow is DaggerfallPopupWindow && (topWindow as DaggerfallPopupWindow).PreviousWindow is DaggerfallUnitySaveGameWindow) ||
                            topWindow is DaggerfallLoadClassicGameWindow)
                    return MusicPlaylist.MainMenu;
                else
                    return MusicPlaylist.CharCreation;
            }

            if (!gameLoaded || !playerEnterExit || !localPlayerGPS || !dfUnity || topWindow is DaggerfallVidPlayerWindow)
                return MusicPlaylist.None;
            if (playerEntity.Arrested)
                return MusicPlaylist.Court;
            var musicEnvironment = MusicEnvironment.Wilderness;
            // Exteriors
            if (!playerEnterExit.IsPlayerInside)
            {
                if (localPlayerGPS.IsPlayerInLocationRect)
                {
                    switch (localPlayerGPS.CurrentLocationType)
                    {
                        case DFRegion.LocationTypes.DungeonKeep:
                        case DFRegion.LocationTypes.DungeonLabyrinth:
                        case DFRegion.LocationTypes.DungeonRuin:
                        case DFRegion.LocationTypes.Coven:
                        case DFRegion.LocationTypes.HomePoor:
                            musicEnvironment = MusicEnvironment.DungeonExterior;
                            break;
                        case DFRegion.LocationTypes.Graveyard:
                            musicEnvironment = MusicEnvironment.Graveyard;
                            break;
                        case DFRegion.LocationTypes.HomeFarms:
                        case DFRegion.LocationTypes.HomeWealthy:
                        case DFRegion.LocationTypes.Tavern:
                        case DFRegion.LocationTypes.TownCity:
                        case DFRegion.LocationTypes.TownHamlet:
                        case DFRegion.LocationTypes.TownVillage:
                        case DFRegion.LocationTypes.ReligionTemple:
                            musicEnvironment = MusicEnvironment.City;
                            break;
                        default:
                            musicEnvironment = MusicEnvironment.Wilderness;
                            break;
                    }
                }
                else
                {
                    musicEnvironment = MusicEnvironment.Wilderness;
                }
            }
            // Dungeons
            else if (playerEnterExit.IsPlayerInsideDungeon)
            {
                if (playerEnterExit.IsPlayerInsideDungeonCastle)
                    musicEnvironment = MusicEnvironment.Castle;
                else
                    musicEnvironment = MusicEnvironment.DungeonInterior;
            }
            // Interiors
            else if (playerEnterExit.IsPlayerInside)
            {
                switch (playerEnterExit.BuildingType)
                {
                    case DFLocation.BuildingTypes.Alchemist:
                    case DFLocation.BuildingTypes.Armorer:
                    case DFLocation.BuildingTypes.Bank:
                    case DFLocation.BuildingTypes.Bookseller:
                    case DFLocation.BuildingTypes.ClothingStore:
                    case DFLocation.BuildingTypes.FurnitureStore:
                    case DFLocation.BuildingTypes.GemStore:
                    case DFLocation.BuildingTypes.GeneralStore:
                    case DFLocation.BuildingTypes.Library:
                    case DFLocation.BuildingTypes.PawnShop:
                    case DFLocation.BuildingTypes.WeaponSmith:
                        musicEnvironment = MusicEnvironment.Shop;
                        break;
                    case DFLocation.BuildingTypes.Tavern:
                        musicEnvironment = MusicEnvironment.Tavern;
                        break;
                    case DFLocation.BuildingTypes.GuildHall:
                        if (playerEnterExit.FactionID == (int)FactionFile.FactionIDs.The_Mages_Guild)
                            musicEnvironment = MusicEnvironment.MagesGuild;
                        else
                            musicEnvironment = MusicEnvironment.Interior;
                        break;
                    case DFLocation.BuildingTypes.Palace:
                        musicEnvironment = MusicEnvironment.Palace;
                        break;
                    case DFLocation.BuildingTypes.Temple:
                        musicEnvironment = MusicEnvironment.Temple;
                        break;
                    default:
                        musicEnvironment = MusicEnvironment.Interior;
                        break;
                }
            }

            if (musicEnvironment == MusicEnvironment.City || musicEnvironment == MusicEnvironment.Wilderness)
            {
                if (dfUnity.WorldTime.Now.IsNight)
                    return MusicPlaylist.Night;
                else
                    switch (playerWeather.WeatherType)
                    {
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Cloudy:
                            return MusicPlaylist.Cloudy;
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Overcast:
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Fog:
                            return MusicPlaylist.Overcast;
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Rain:
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Thunder:
                            return MusicPlaylist.Rain;
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Snow:
                            return MusicPlaylist.Snow;
                        default:
                            return MusicPlaylist.Sunny;
                    }
            }
            else
                switch (musicEnvironment)
                {
                    case MusicEnvironment.Castle:
                        return MusicPlaylist.Castle;
                    case MusicEnvironment.DungeonExterior:
                        return MusicPlaylist.Night;
                    case MusicEnvironment.DungeonInterior:
                        return MusicPlaylist.DungeonInterior;
                    case MusicEnvironment.Graveyard:
                        return MusicPlaylist.Night;
                    case MusicEnvironment.MagesGuild:
                        return MusicPlaylist.MagesGuild;
                    case MusicEnvironment.Interior:
                        return MusicPlaylist.Interior;
                    case MusicEnvironment.Palace:
                        return MusicPlaylist.Palace;
                    case MusicEnvironment.Shop:
                        return MusicPlaylist.Shop;
                    case MusicEnvironment.Tavern:
                        return MusicPlaylist.Tavern;
                    case MusicEnvironment.Temple:
                        return MusicPlaylist.Temple;
                    default:
                        return MusicPlaylist.None;
                }
        }

        // Adapted from SongManager.cs. Credit to Interkarma.
        private static SongFiles GetSong(MusicPlaylist musicPlaylist)
        { 
            var index = 0;
            // Map playlist enum to SongManager playlist.
            SongFiles[] currentPlaylist;
            switch (musicPlaylist)
            {
                case MusicPlaylist.Night:
                    currentPlaylist = Instance.NightSongs;
                    break;
                case MusicPlaylist.Sunny:
                    currentPlaylist = Instance.SunnySongs;
                    break;
                case MusicPlaylist.Cloudy:
                    currentPlaylist = Instance.CloudySongs;
                    break;
                case MusicPlaylist.Overcast:
                    currentPlaylist = Instance.OvercastSongs;
                    break;
                case MusicPlaylist.Rain:
                    currentPlaylist = Instance.RainSongs;
                    break;
                case MusicPlaylist.Snow:
                    currentPlaylist = Instance.SnowSongs;
                    break;
                case MusicPlaylist.Temple:
                    currentPlaylist = Instance.TempleSongs;
                    break;
                case MusicPlaylist.Tavern:
                    currentPlaylist = Instance.TavernSongs;
                    break;
                case MusicPlaylist.Shop:
                    currentPlaylist = Instance.ShopSongs;
                    break;
                case MusicPlaylist.DungeonInterior:
                    currentPlaylist = Instance.DungeonInteriorSongs;
                    break;
                case MusicPlaylist.MagesGuild:
                    currentPlaylist = Instance.MagesGuildSongs;
                    break;
                case MusicPlaylist.Interior:
                    currentPlaylist = Instance.InteriorSongs;
                    break;
                case MusicPlaylist.Palace:
                    currentPlaylist = Instance.PalaceSongs;
                    break;
                case MusicPlaylist.Castle:
                    currentPlaylist = Instance.CastleSongs;
                    break;
                case MusicPlaylist.Court:
                    currentPlaylist = Instance.CourtSongs;
                    break;
                default:
                    currentPlaylist = null;
                    break;
            };

            if (currentPlaylist == null)
            {
                return SongFiles.song_none;
            }

            // General MIDI song selection
            {
                uint gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
                DFRandom.srand(gameMinutes / 1440);
                uint random = DFRandom.rand();
                if (currentPlaylist == Instance.NightSongs)
                    index = (int)(random % Instance.NightSongs.Length);
                else if (currentPlaylist == Instance.SunnySongs)
                    index = (int)(random % Instance.SunnySongs.Length);
                else if (currentPlaylist == Instance.CloudySongs)
                    index = (int)(random % Instance.CloudySongs.Length);
                else if (currentPlaylist == Instance.OvercastSongs)
                    index = (int)(random % Instance.OvercastSongs.Length);
                else if (currentPlaylist == Instance.RainSongs)
                    index = (int)(random % Instance.RainSongs.Length);
                else if (currentPlaylist == Instance.SnowSongs)
                    index = (int)(random % Instance.SnowSongs.Length);
                else if (currentPlaylist == Instance.TempleSongs && Instance.playerEnterExit)
                {
                    byte[] templeFactions = { 0x52, 0x54, 0x58, 0x5C, 0x5E, 0x62, 0x6A, 0x24 };
                    uint factionOfPlayerEnvironment = Instance.playerEnterExit.FactionID;
                    index = Array.IndexOf(templeFactions, (byte)factionOfPlayerEnvironment);
                    if (index < 0)
                    {
                        byte[] godFactions = { 0x15, 0x16, 0x18, 0x1A, 0x1B, 0x1D, 0x21, 0x23 };
                        index = Array.IndexOf(godFactions, (byte)factionOfPlayerEnvironment);
                    }
                }
                else if (currentPlaylist == Instance.TavernSongs)
                {
                    index = (int)(gameMinutes / 1440 % Instance.TavernSongs.Length);
                }
                else if (currentPlaylist == Instance.DungeonInteriorSongs)
                {
                    PlayerGPS gps = GameManager.Instance.PlayerGPS;
                    ushort unknown2 = 0;
                    int region = 0;
                    if (gps.HasCurrentLocation)
                    {
                        unknown2 = (ushort)gps.CurrentLocation.Dungeon.RecordElement.Header.Unknown2;
                        region = gps.CurrentRegionIndex;
                    }

                    DFRandom.srand(unknown2 ^ ((byte)region << 8));
                    random = DFRandom.rand();
                    index = (int)(random % Instance.DungeonInteriorSongs.Length);
                }
                else if (currentPlaylist == Instance.MagesGuildSongs/* || currentPlaylist == MusicPlaylist.Sneaking*/)
                {
                    index = Instance.randomIndRequested ? UnityEngine.Random.Range(0, Instance.MagesGuildSongs.Length) : index;
                    Instance.randomIndRequested = false;
                }
            }

            return currentPlaylist[index];
        }

        private void GetDebuggingText(string track, out string playlistName, out string songName, bool isUserDefined)
        {
            var custom = isUserDefined ? " (User-Defined)" : "";
            playlistName = $"{Path.GetFileName(Path.GetDirectoryName(track))}{custom}";
            songName = Path.GetFileName(track);
        }

        private void GetDebuggingText(SongFiles song, out string playlistName, out string songName)
        {
            playlistName = ((MusicPlaylist)currentPlaylist).ToString();
            songName = song.ToString();
        }

        private bool GetCombatStatus(out int maxLevel)
        {
            bool isEnemyFound = false;
            int maxLevel_ = 0;
            var entityBehaviours = FindObjectsOfType<DaggerfallEntityBehaviour>();
            foreach (var entityBehaviour in entityBehaviours)
            {
                if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
                {
                    var enemySenses = entityBehaviour.GetComponent<EnemySenses>();
                    if (enemySenses && enemySenses.Target == gameManager.PlayerEntityBehaviour && enemySenses.DetectedTarget && enemySenses.TargetInSight)
                    {
                        isEnemyFound = true;
                        if (entityBehaviour.Entity.Level > maxLevel_)
                            maxLevel_ = entityBehaviour.Entity.Level;
                    }
                }
            }

            maxLevel = maxLevel_;
            return isEnemyFound;
        }

        // Load new location's song player when player moves into it.
        private void OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private void OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private void OnTransitionDungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private void OnTransitionDungeonExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private static void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            Instance.HandleLocationChange();
            Instance.isInCombat = false;
            Instance.gameLoaded = true;
        }

        private static void StartGameBehaviour_OnStartGame(object sender, EventArgs e)
        {
            Instance.HandleLocationChange();
            Instance.gameLoaded = true;
        }

        private void OnDeath(DaggerfallEntity entity)
        {
            // Fade out on death.
            combatTaper = 0;
            currentState = State.FadingOut;
            currentPlaylist = (int)MusicPlaylist.None;
            isInCombat = false;
            gameLoaded = false;
        }
    }
}
