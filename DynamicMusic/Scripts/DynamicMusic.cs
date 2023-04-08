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

namespace DynamicMusic
{
    [RequireComponent(typeof(DynamicSongPlayer))]
    public sealed class DynamicMusic : MonoBehaviour
    {
        #region Playlists
        private enum MusicPlaylist
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
            Combat,
            None
        }

        private enum MusicEnvironment
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

        private enum State
        {
            Normal,
            FadingOut,
            FadingIn,
            Combat
        }

        private enum MusicType
        {
            Normal,
            Combat
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

        private sealed class Playlist
        {
            private readonly List<string> tracks;
            private int index = 0;
            private void ShuffleTracks()
            {
                var endTrack = tracks[tracks.Count - 1];
                // Fisher-Yates shuffle
                for (var i = tracks.Count - 1; i > 0; i--)
                {
                    var randIndex = UnityEngine.Random.Range(0, i + 1);
                    (tracks[randIndex], tracks[i]) = (tracks[i], tracks[randIndex]);
                }

                // Ensure that 0 doesn't swap with n-1. Otherwise there will be a repeat on re-shuffle.
                if (tracks[0] == endTrack)
                    (tracks[0], tracks[tracks.Count - 1]) = (tracks[tracks.Count - 1], tracks[0]);
            }

            public Playlist(List<string> trackList)
            {
                tracks = trackList;
                ShuffleTracks();
            }

            public string GetNextTrack()
            {
                index = (index + 1) % tracks.Count;
                if (index == 0)
                    ShuffleTracks();
                return tracks[index];
            }


            public int TrackCount => tracks.Count;
            public string CurrentTrack => tracks[index];
        }

        public static DynamicMusic Instance { get; private set; }
        private static Mod mod;
        private PlayerGPS localPlayerGPS;
        private PlayerEnterExit playerEnterExit;
        private PlayerWeather playerWeather;
        private DynamicSongPlayer dynamicSongPlayer;
        private GameManager gameManager;
        private PlayerEntity playerEntity;
        private float detectionCheckInterval;
        private float detectionCheckDelta;
        private float fadeOutLength;
        private float fadeInLength;
        private float fadeOutTime;
        private float fadeInTime;
        private byte combatTaperLength;
        private byte combatTaper;
        private SongFiles[] defaultCombatSongs;
        private Playlist combatPlaylist;
        private Playlist[] customPlaylists;
        private string currentCustomTrack;
        private byte combatPlaylistIndex;
        private string combatMusicPath;
        private bool combatMusicIsMidi;
        private float previousTimeSinceStartup;
        private float deltaTime;
        private bool gameLoaded;
        private MusicPlaylist currentPlaylist = MusicPlaylist.None;
        private bool normalSongQueued;
        private State currentState;
        private State lastState;
        private MusicType currentMusicType;
        private const string soundDirectory = "Sound";
        private const string baseDirectory = "DynMusic";
        private const string fileSearchPattern = "*.ogg";

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<DynamicMusic>();
            DontDestroyOnLoad(go);
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            StartGameBehaviour.OnStartGame += StartGameBehaviour_OnStartGame;
            //mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        private void Awake()
        {
            //var settings = mod.GetSettings();
            //LoadSettings(settings, new ModSettingsChange());
            var soundPath = Path.Combine(Application.streamingAssetsPath, soundDirectory);
            customPlaylists = new Playlist[(int)MusicPlaylist.None];
            for (var i = 0; i < customPlaylists.Length; i++)
            {
                var musicPath = Path.Combine(soundPath, baseDirectory, $"{(MusicPlaylist)i}");
                if (!Directory.Exists(musicPath))
                {
                    Directory.CreateDirectory(musicPath);
                    continue;
                }

                var files = Directory.GetFiles(musicPath, fileSearchPattern);
                if (files.Length == 0)
                    continue;
                var trackList = new List<string>();
                foreach (var fileName in files)
                    trackList.Add(fileName);
                customPlaylists[i] = new Playlist(trackList);
            }

            dynamicSongPlayer = GetComponent<DynamicSongPlayer>();
            combatMusicPath = Path.Combine(soundPath, baseDirectory, $"{MusicPlaylist.Combat}");
            combatPlaylist = customPlaylists[(int)MusicPlaylist.Combat];
            previousTimeSinceStartup = Time.realtimeSinceStartup;
            gameLoaded = false;
            Debug.Log("Dynamic Music initialized.");
            mod.IsReady = true;
        }

        // Load settings that can change during runtime.
        /*
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
        }
        */

        private void Start()
        {
            // Remove vanilla song players from memory.
            var unityObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
            foreach (var unityObject in unityObjects)
            {
                if (unityObject.name == "SongPlayer")
                    Destroy(unityObject);
            }

            gameManager = GameManager.Instance;
            playerEntity = gameManager.PlayerEntity;
            localPlayerGPS = gameManager.PlayerGPS;
            playerEnterExit = localPlayerGPS.GetComponent<PlayerEnterExit>();
            playerWeather = localPlayerGPS.GetComponent<PlayerWeather>();
            detectionCheckInterval = 3f;
            combatTaperLength = 4;
            fadeOutLength = 2f;
            fadeInLength = 3f;
            currentState = State.Normal;
            lastState = currentState;
            currentMusicType = MusicType.Normal;
            defaultCombatSongs = new SongFiles[]
            {
                SongFiles.song_17, // fighter trainers
                SongFiles.song_30  // unused sneaking (?) theme
            };

            // Use alternate music if set
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

            combatPlaylistIndex = (byte)UnityEngine.Random.Range(0, combatPlaylist == null ? defaultCombatSongs.Length : combatPlaylist.TrackCount);
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            playerEntity.OnDeath += OnDeath;
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
            currentPlaylist = GetMusicPlaylist(localPlayerGPS, playerEnterExit, playerWeather);
            switch (currentState)
            {
                case State.Normal:
                    // Fade out when switching playlists.
                    if (currentPlaylist != previousPlaylist && previousPlaylist != MusicPlaylist.None)
                    {
                        currentState = State.FadingOut;
                        break;
                    }

                    if (currentPlaylist == MusicPlaylist.None)
                    {
                        if (dynamicSongPlayer.IsPlaying)
                            dynamicSongPlayer.Stop();
                        break;
                    }

                    dynamicSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                    var currentCustomPlaylist = customPlaylists[(int)currentPlaylist] != null ? currentPlaylist : MusicPlaylist.None;
                    // Plays random tracks continuously as long as custom tracks are available for the current context.
                    if (currentCustomPlaylist != MusicPlaylist.None &&
                        (currentCustomTrack != customPlaylists[(int)currentPlaylist].CurrentTrack || // Changed to a different custom track.
                            !dynamicSongPlayer.AudioSource.isPlaying))                               // Current custom track reached its end.
                    {
                        dynamicSongPlayer.AudioSource.loop = false;
                        var playlist = customPlaylists[(int)currentCustomPlaylist];
                        var audioSource = dynamicSongPlayer.AudioSource;
                        var path = Path.Combine(Application.streamingAssetsPath, soundDirectory);
                        if (TryLoadSong(path, playlist.GetNextTrack(), out var song))
                        {
                            currentCustomTrack = playlist.CurrentTrack;
                            audioSource.clip = song;
                            audioSource.Play();
                        }
                    }
                    // Loop the music as usual if no custom soundtracks are found.
                    else if (currentCustomPlaylist == MusicPlaylist.None && GetSong(currentPlaylist, out var song) && song != dynamicSongPlayer.Song /*&& !songManager.enabled*/)
                    {
                        dynamicSongPlayer.Play(song);
                        dynamicSongPlayer.AudioSource.loop = true;
                        currentCustomTrack = string.Empty; // Should not be a custom track set if one is not playing.
                    }

                    break;
                case State.FadingOut:
                    if (currentMusicType == MusicType.Normal)
                    {
                        fadeOutTime += deltaTime;
                        dynamicSongPlayer.AudioSource.volume = Mathf.Lerp(DaggerfallUnity.Settings.MusicVolume, 0f, fadeOutTime / fadeOutLength);
                        if (fadeOutTime >= fadeOutLength)
                        {
                            // End fade when time elapsed.
                            fadeOutTime = 0f;
                            currentState = State.Normal;
                        }
                    }
                    else if (currentMusicType == MusicType.Combat)
                    {
                        fadeOutTime += deltaTime;
                        dynamicSongPlayer.AudioSource.volume = Mathf.Lerp(DaggerfallUnity.Settings.MusicVolume, 0f, fadeOutTime / fadeOutLength);
                        // End fade when time elapsed.
                        if (fadeOutTime >= fadeOutLength)
                        {
                            StopCombatMusic();
                            fadeOutTime = 0f;
                            currentMusicType = MusicType.Normal;
                            currentState = State.Normal;
                        }
                    }

                    break;
                case State.Combat:
                    {
                        // Handle volume/looping.
                        dynamicSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                        if (combatMusicIsMidi && !dynamicSongPlayer.IsPlaying)
                            dynamicSongPlayer.Play(); // Loop combat music if MIDI.
                                                      // Start combat music if not playing.
                        dynamicSongPlayer.AudioSource.loop = true;
                        if (lastState != State.Combat)
                        {
                            var songFile = defaultCombatSongs[combatPlaylistIndex % defaultCombatSongs.Length];
                            if (combatPlaylist != null && TryLoadSong(combatMusicPath, combatPlaylist.GetNextTrack(), out var song))
                            {
                                dynamicSongPlayer.AudioSource.clip = song;
                                dynamicSongPlayer.AudioSource.Play();
                                combatMusicIsMidi = false;
                            }
                            else
                            {
                                dynamicSongPlayer.Play(songFile);
                                combatMusicIsMidi = dynamicSongPlayer.AudioSource.clip == null;
                            }

                            var playlistCount = defaultCombatSongs.Length;
                            dynamicSongPlayer.Song = songFile;
                            combatPlaylistIndex += (byte)UnityEngine.Random.Range(1, playlistCount - 1);
                            combatPlaylistIndex %= (byte)playlistCount;
                            lastState = State.Combat;
                        }

                        // Fade out on arrest.
                        if (playerEntity.Arrested)
                            currentState = State.FadingOut;
                    }

                    break;
            }

            // Check if player is in combat at every interval.
            detectionCheckDelta += Time.deltaTime;
            if (detectionCheckDelta < detectionCheckInterval)
                return;
            if (currentState == State.Normal || currentState == State.Combat)
            {
                if (!gameManager.PlayerDeath.DeathInProgress && !playerEntity.Arrested && IsPlayerDetected())
                {
                    // Start combat music
                    lastState = currentState;
                    currentState = State.Combat;
                    combatTaper = combatTaperLength;
                    currentMusicType = MusicType.Combat;
                }
                else if (combatTaper > 0 && --combatTaper == 0)
                    currentState = State.FadingOut;
            }

            detectionCheckDelta = 0f;
        }

        private bool IsPlayerDetected()
        {
            var entityBehaviours = FindObjectsOfType<DaggerfallEntityBehaviour>();
            foreach (var entityBehaviour in entityBehaviours)
            {
                if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
                {
                    var enemySenses = entityBehaviour.GetComponent<EnemySenses>();
                    if (enemySenses && enemySenses.Target == gameManager.PlayerEntityBehaviour && enemySenses.DetectedTarget && enemySenses.TargetInSight)
                        return true;
                }
            }

            return false;
        }

        private void HandleLocationChange()
        {
            if (currentState == State.Combat)
            {
                currentState = State.FadingOut;
                combatTaper = 0;
            }

            //currentPlaylist = MusicPlaylist.None; // Clear previous playlist.
        }

        private bool TryLoadSong(string soundPath, string name, out AudioClip audioClip)
        {
            string path = Path.Combine(soundPath, name);
            if (File.Exists(path))
            {
                var www = new WWW("file://" + path); // the "non-deprecated" class gives me compiler errors so it can suck it
                audioClip = www.GetAudioClip(true, true);
                return audioClip != null;
            }

            audioClip = null;
            return false;
        }

        private void StopCombatMusic()
        {
            fadeOutTime = 0f;
            dynamicSongPlayer.AudioSource.loop = false;
            dynamicSongPlayer.Stop(); // stop midi in case it's playing
            combatMusicIsMidi = false;
            dynamicSongPlayer.AudioSource.Stop();
        }

        private MusicPlaylist GetMusicPlaylist(PlayerGPS localPlayerGPS, PlayerEnterExit playerEnterExit, PlayerWeather playerWeather)
        {
            // Note: Code was adapted from SongManager.cs. Credit goes to Interkarma for the original implementation.
            var dfUnity = DaggerfallUnity.Instance;
            var topWindow = DaggerfallUI.UIManager.TopWindow;
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
                    musicEnvironment = MusicEnvironment.Wilderness;
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
        private static bool GetSong(MusicPlaylist musicPlaylist, out SongFiles song)
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
                song = SongFiles.song_none;
                return false;
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
                    index = UnityEngine.Random.Range(0, Instance.MagesGuildSongs.Length);
                }
            }

            song = currentPlaylist[index];
            return true;
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
            Instance.lastState = State.Normal;
            Instance.gameLoaded = true;
        }

        private static void StartGameBehaviour_OnStartGame(object sender, EventArgs e)
        {
            Instance.HandleLocationChange();
            Instance.lastState = State.Normal;
            Instance.gameLoaded = true;
        }

        private void OnDeath(DaggerfallEntity entity)
        {
            // Fade out on death.
            currentState = State.FadingOut;
            currentPlaylist = MusicPlaylist.None;
            gameLoaded = false;
        }
    }
}
