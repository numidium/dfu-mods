using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DynamicMusic
{
    [RequireComponent(typeof(DaggerfallSongPlayer))]
    public sealed class DynamicMusic : MonoBehaviour
    {
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

        public static DynamicMusic Instance { get; private set; }
        private static Mod mod;
        private SongManager songManager;
        private DaggerfallSongPlayer combatSongPlayer;
        private GameManager gameManager;
        private float stateChangeInterval;
        private float stateCheckDelta;
        private float fadeOutLength;
        private float fadeInLength;
        private float fadeOutTime;
        private float fadeInTime;
        private byte taperOffLength;
        private byte taperFadeStart;
        private byte taperOff;
        private SongFiles[] defaultCombatSongs;
        private List<string> combatPlaylist;
        private List<string>[] customPlaylists;
        private byte combatPlaylistIndex;
        private string combatMusicPath;
        private bool combatMusicIsMidi;
        private byte normalPlaylistIndex;
        private MusicPlaylist currentCustomPlaylist = MusicPlaylist.None;
        private const string soundDirectory = "Sound";
        private const string directoryPrefix = "DynMusic_";
        private const string fileSearchPattern = "*.ogg";

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<DynamicMusic>();
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            //mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        private void Awake()
        {
            //var settings = mod.GetSettings();
            //LoadSettings(settings, new ModSettingsChange());
            var soundPath = Path.Combine(Application.streamingAssetsPath, soundDirectory);
            customPlaylists = new List<string>[(int)MusicPlaylist.None];
            for (var i = 0; i < customPlaylists.Length; i++)
            {
                var musicPath = Path.Combine(soundPath, $"{directoryPrefix}{(MusicPlaylist)i}");
                if (!Directory.Exists(musicPath))
                    continue;
                var files = Directory.GetFiles(musicPath, fileSearchPattern);
                customPlaylists[i] = new List<string>();
                foreach (var fileName in files)
                    customPlaylists[i].Add(fileName);
            }

            combatSongPlayer = GetComponent<DaggerfallSongPlayer>();
            combatMusicPath = Path.Combine(soundPath, $"{directoryPrefix}{MusicPlaylist.Combat}");
            combatPlaylist = customPlaylists[(int)MusicPlaylist.Combat];
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
            gameManager = GameManager.Instance;
            stateChangeInterval = 3f;
            taperOffLength = 5;
            taperFadeStart = 1;
            fadeOutLength = 1f;
            fadeInLength = 2f;
            defaultCombatSongs = new SongFiles[]
            {
                SongFiles.song_17, // fighter trainers
                SongFiles.song_30  // unused sneaking (?) theme
            };

            combatPlaylistIndex = (byte)Random.Range(0, combatPlaylist.Count);
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            gameManager.PlayerEntity.OnDeath += OnDeath;
        }

        private void Update()
        {
            if (!songManager)
                return;
            stateCheckDelta += Time.deltaTime;
            // Fade out combat music.
            if (fadeOutTime > 0f)
            {
                // Fade out combat music during taper time.
                fadeOutTime += Time.deltaTime;
                combatSongPlayer.AudioSource.volume = Mathf.Lerp(DaggerfallUnity.Settings.MusicVolume, 0f, fadeOutTime / fadeOutLength);
                // End fade when time elapsed.
                if (fadeOutTime >= fadeOutLength)
                    StopCombatMusic();
            }
            else if (combatMusicIsMidi && !combatSongPlayer.IsPlaying)
                combatSongPlayer.Play(); // Loop combat music if MIDI.
            var songPlayer = songManager.SongPlayer;
            // Current song in custom playlist is finished, play another.
            if (currentCustomPlaylist != MusicPlaylist.None && !songPlayer.AudioSource.isPlaying && fadeOutTime == 0f)
            {
                var playlist = customPlaylists[(int)currentCustomPlaylist];
                var index = (byte)Random.Range(0, playlist.Count - 1);
                var path = Path.Combine(Application.streamingAssetsPath, soundDirectory);
                TryLoadSong(path, playlist[index], out var song);
                songManager.SongPlayer.AudioSource.clip = song;
                songManager.SongPlayer.AudioSource.Play();
            }

            // Fade in normal music.
            if (fadeInTime > 0f && songPlayer.AudioSource.isPlaying)
            {
                fadeInTime += Time.deltaTime;
                if (songPlayer.enabled)
                    songPlayer.enabled = false; // Stop SongPlayer from controlling its own volume.
                songPlayer.AudioSource.volume = Mathf.Lerp(0f, DaggerfallUnity.Settings.MusicVolume, fadeInTime / fadeInLength);
                if (fadeInTime >= fadeInLength)
                {
                    fadeInTime = 0f; // End fade when time elapsed.
                    // Resume updates in SongPlayer if custom playlist not loaded.
                    songPlayer.enabled = currentCustomPlaylist == MusicPlaylist.None || customPlaylists[(int)currentCustomPlaylist] == null;
                }
            }

            // Only perform state check once per assigned interval.
            if (stateCheckDelta < stateChangeInterval)
                return;
            if (!gameManager.PlayerDeath.DeathInProgress && IsPlayerDetected())
            {
                // Switch to combat music if not tapering or already playing.
                if (taperOff == 0 || !IsCombatMusicPlaying())
                {
                    songManager.StopPlaying();
                    songManager.enabled = false;
                    songPlayer.enabled = false;
                    combatSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                    combatSongPlayer.AudioSource.loop = true;
                    int playlistCount;
                    if (combatPlaylist != null && TryLoadSong(combatMusicPath, combatPlaylist[combatPlaylistIndex], out var song))
                    {
                        combatSongPlayer.AudioSource.clip = song;
                        combatSongPlayer.AudioSource.Play();
                        playlistCount = combatPlaylist.Count;
                        combatMusicIsMidi = false;
                    }
                    else
                    {
                        var songFile = defaultCombatSongs[combatPlaylistIndex % defaultCombatSongs.Length];
                        combatSongPlayer.Play(songFile);
                        combatSongPlayer.Song = songFile;
                        playlistCount = defaultCombatSongs.Length;
                        combatMusicIsMidi = combatSongPlayer.AudioSource.clip == null;
                    }

                    combatPlaylistIndex += (byte)Random.Range(1, playlistCount - 1);
                    combatPlaylistIndex %= (byte)playlistCount;
                }

                taperOff = taperOffLength;
            }
            else if (taperOff <= taperFadeStart && (combatSongPlayer.AudioSource.isPlaying || (combatMusicIsMidi && combatSongPlayer.IsPlaying)))
                fadeOutTime = Time.deltaTime; // Begin fading after taper ends.
            else if (taperOff > 0 && --taperOff == 0)
            {
                List<string> playlist = currentCustomPlaylist == MusicPlaylist.None ? null : customPlaylists[(int)currentCustomPlaylist];
                var index = playlist == null ? 0 : (byte)Random.Range(0, playlist.Count - 1);
                var path = Path.Combine(Application.streamingAssetsPath, soundDirectory);
                if (currentCustomPlaylist != MusicPlaylist.None && playlist != null && TryLoadSong(path, playlist[index], out var song))
                {
                    songManager.SongPlayer.AudioSource.clip = song;
                    songManager.SongPlayer.AudioSource.Play();
                }
                else
                {
                    // Re-enable vanilla music system on taper end.
                    songManager.SongPlayer.enabled = true;
                    songManager.enabled = true;
                    songManager.StartPlaying();
                }

                fadeInTime = Time.deltaTime; // Start normal music fade-in.
            }

            #if UNITY_EDITOR
            if (taperOff > 0)
                Debug.Log("DynamicMusic: taperOff = " + taperOff);
            #endif
            stateCheckDelta = 0f;
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

        private void LoadSongManager()
        {
            var go = GameObject.Find("SongPlayer");
            songManager = go.GetComponent<SongManager>();
            var localPlayerGPS = songManager.LocalPlayerGPS;
            var playlist = GetMusicPlaylist(localPlayerGPS, localPlayerGPS.GetComponent<PlayerEnterExit>(), localPlayerGPS.GetComponent<PlayerWeather>());
            currentCustomPlaylist = customPlaylists[(int)playlist] != null ? playlist : MusicPlaylist.None;
            if (currentCustomPlaylist == MusicPlaylist.None || customPlaylists[(int)currentCustomPlaylist] == null)
            {
                songManager.SongPlayer.enabled = true;
                songManager.enabled = true;
                if (!songManager.SongPlayer.IsPlaying)
                    songManager.StartPlaying();
            }
            else
            {
                songManager.SongPlayer.enabled = false;
                songManager.SongPlayer.AudioSource.loop = false;
                songManager.enabled = false;
            }

            taperOff = 0; // Don't continue tapering if we have a freshly loaded player.
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
            combatSongPlayer.AudioSource.loop = false;
            combatSongPlayer.Stop(); // stop midi in case it's playing
            combatMusicIsMidi = false;
            combatSongPlayer.AudioSource.Stop();
        }

        private bool IsCombatMusicPlaying()
        {
            return (combatMusicIsMidi && combatSongPlayer.IsPlaying) ||
                (!combatMusicIsMidi && combatSongPlayer.AudioSource.isPlaying);
        }

        private MusicPlaylist GetMusicPlaylist(PlayerGPS localPlayerGPS, PlayerEnterExit playerEnterExit, PlayerWeather playerWeather)
        {
            // Note: Code was adapted from SongManager.cs. Credit goes to Interkarma for the original implementation.
            var dfUnity = DaggerfallUnity.Instance;
            if (!playerEnterExit || !localPlayerGPS || !dfUnity)
                return MusicPlaylist.None;
            if (gameManager.PlayerEntity.Arrested)
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

        // Load new location's song player when player moves into it.
        private void OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            LoadSongManager();
        }

        private void OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            LoadSongManager();
        }

        private void OnTransitionDungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            LoadSongManager();
        }

        private void OnTransitionDungeonExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            LoadSongManager();
        }

        private static void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            Instance.LoadSongManager();
        }

        private void OnDeath(DaggerfallEntity entity)
        {
            // Fade out on death.
            if (IsCombatMusicPlaying())
                fadeOutTime = Time.deltaTime;
        }
    }
}
