using DaggerfallConnect;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using FullSerializer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Wenzil.Console;

namespace DynamicAmbience
{
    public sealed class DynamicAmbience : MonoBehaviour
    {
        private enum State : byte
        {
            Normal,
            FadingOut,
            FadingIn
        }

        /// <summary>
        /// Holds a list of strings that represent music tracks. Advances track and auto shuffles.
        /// </summary>
        private sealed class Playlist
        {
            private readonly string[] tracks;
            private int index = 0;
            public int TrackCount => tracks.Length;
            public string CurrentTrack => tracks[index];
            public float MinDelay { get; private set; }
            public float MaxDelay { get; private set; }

            public Playlist(string[] trackList, float minDelay, float maxDelay)
            {
                tracks = trackList;
                MinDelay = minDelay;
                MaxDelay = maxDelay;
                ShuffleTracks();
            }

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

            public string GetNextTrack()
            {
                index = (index + 1) % tracks.Length;
                if (index == 0)
                    ShuffleTracks();
                return tracks[index];
            }

        }

        private sealed class AmbientAudioSource
        {
            public AudioSource AudioSource { get; private set; }
            private bool clipFileLoaded;
            private float timeSinceLastPlay;
            private float delay;
            public bool IsReady
            { 
                get
                {
                    return clipFileLoaded && AudioSource.clip != null && AudioSource.clip.loadState == AudioDataLoadState.Loaded;
                }
            }
            private AudioClip oldClip;
            private Playlist playlist;

            public AmbientAudioSource(AudioSource audioSource)
            {
                AudioSource = audioSource;
            }

            public void SetPlaylist(Playlist playlist_)
            {
                playlist = playlist_;
                delay = UnityEngine.Random.Range(playlist.MinDelay, playlist.MaxDelay);
                clipFileLoaded = false;
                Instance.StartCoroutine(StartClip(playlist.GetNextTrack()));
            }

            public void Step()
            {
                if (playlist == null)
                    return;
                timeSinceLastPlay += Time.unscaledDeltaTime;
                if (IsReady && AudioSource.clip.loadState == AudioDataLoadState.Loaded && !AudioSource.isPlaying
                        && timeSinceLastPlay >= delay)
                {
                    AudioSource.Play();
                    timeSinceLastPlay = 0f;
                    if (playlist.MaxDelay > 0f)
                        delay = UnityEngine.Random.Range(playlist.MinDelay, playlist.MaxDelay);
                    else
                        delay = playlist.MinDelay;
                }
            }

            private IEnumerator StartClip(string path)
            {
                using (var request = UnityWebRequestMultimedia.GetAudioClip($"file://{path}", AudioType.OGGVORBIS))
                {
                    yield return request.SendWebRequest();
                    if (request.responseCode != 200)
                        AudioSource.clip = null;
                    else
                    {
                        if (AudioSource.clip)
                        {
                            oldClip = AudioSource.clip;
                            if (oldClip.UnloadAudioData())
                                Destroy(oldClip);
                            else 
                                PrintLog("Failed to unload audio clip.");
                        }

                        clipFileLoaded = true;
                        AudioSource.clip = DownloadHandlerAudioClip.GetContent(request);
                    }
                }
            }
        }

        public static DynamicAmbience Instance { get; private set; }
        private static Mod mod;
        private DaggerfallUnity daggerfallUnity;
        private PlayerGPS localPlayerGPS;
        private PlayerEnterExit playerEnterExit;
        private PlayerWeather playerWeather;
        private AmbiencePlayer ambiencePlayer;
        private GameManager gameManager;
        private PlayerEntity playerEntity;
        private const float fadeOutLength = 2f;
        private const float fadeInLength = 2f;
        private float fadeOutTime;
        private float fadeInTime;
        private AmbienceRecord[] ambienceRecords;
        private Playlist[] ambiencePlaylists;
        private AmbientAudioSource[] ambientAudioSources;
        private const int maxAmbientAudioSources = 10;
        private bool isInCombat;
        private bool resumeIsEnabled = true;
        private bool gameLoaded;
        private int currentPlaylist;
        private State currentState;
        private bool contextChangeQueued;
        private string debugPlaylistName;
        private string debugTrackName;
        private GUIStyle guiStyle;
        private const string soundDirectory = "Sound";
        private const string baseDirectory = "DynAmbience";
        private const string playlistsDirectory = "Playlists";
        private const string playlistSearchPattern = "*.json";
        private const string soundSearchPattern = "*.ogg";
        private const string modSignature = "Dynamic Ambience";
        private string basePath;
        private string playlistsPath;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<DynamicAmbience>();
            Instance.ambiencePlayer = go.AddComponent<AmbiencePlayer>();
            Instance.ambiencePlayer.ModSignature = modSignature;
            DontDestroyOnLoad(go);
            //SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            //StartGameBehaviour.OnStartGame += StartGameBehaviour_OnStartGame;
            mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        // Load settings that can change during runtime.
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
        }

        private void Start()
        {   
            daggerfallUnity = DaggerfallUnity.Instance;
            gameManager = GameManager.Instance;
            // Load settings that require a restart.
            var settings = mod.GetSettings();
            LoadSettings(settings, new ModSettingsChange());

            // Set references for quick access.
            playerEntity = gameManager.PlayerEntity;
            localPlayerGPS = gameManager.PlayerGPS;
            playerEnterExit = localPlayerGPS.GetComponent<PlayerEnterExit>();
            playerWeather = localPlayerGPS.GetComponent<PlayerWeather>();

            // Set timing variables and state.
            gameLoaded = false;
            currentState = State.FadingIn;
            fadeInTime = 0f;
            currentPlaylist = -1;

            // Setup events.
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            PlayerGPS.OnMapPixelChanged += OnMapPixelChanged;
            PlayerGPS.OnEnterLocationRect += OnEnterLocationRect;
            WorldTime.OnDusk += OnDuskEvent;
            WorldTime.OnDawn += OnDawnEvent;
            playerEntity.OnDeath += OnDeath;
            guiStyle = new GUIStyle();
            guiStyle.normal.textColor = Color.black;
            Debug.Log($"{modSignature} initialized.");
            mod.IsReady = true;

            basePath = Path.Combine(Application.streamingAssetsPath, soundDirectory, baseDirectory);
            playlistsPath = Path.Combine(basePath, playlistsDirectory);           
            if (!Directory.Exists(playlistsPath))
                return;
            var playlistFileNames = Directory.GetFiles(playlistsPath, playlistSearchPattern);
            var serializer = new fsSerializer();
            var records = new List<AmbienceRecord>();
            var playlists = new List<Playlist>();
            List<AmbienceRecord> recordEntries;
            foreach(var playlistFileName in playlistFileNames)
            {
                using (var streamReader = new StreamReader(playlistFileName))
                {
                    var fsResult = fsJsonParser.Parse(streamReader.ReadToEnd(), out var fsData);
                    if (!fsResult.Equals(fsResult.Success))
                        continue;
                    recordEntries = null;
                    serializer.TryDeserialize(fsData, ref recordEntries).AssertSuccess();
                }

                foreach (var entry in recordEntries)
                {
                    if (entry.Name == null || entry.Name == string.Empty)
                    {
                        PrintLog($"Playlist in {playlistFileName} has no name.");
                        continue;
                    }

                    var playlistPath = Path.Combine(basePath, entry.Name);
                    if (!Directory.Exists(playlistPath))
                    {
                        PrintLog($"Could not find directory for playlist: {entry.Name} referenced in file {playlistFileName}.");
                        continue;   
                    }

                    var trackFileNames = Directory.GetFiles(playlistPath, soundSearchPattern);
                    if (trackFileNames.Length == 0)
                    {
                        PrintLog($"Playlist directory {entry.Name} in file {playlistFileName} is empty. Skipping...");
                        continue;
                    }

                    records.Add(entry);
                    playlists.Add(new Playlist(trackFileNames, entry.MinDelay ?? 0, entry.MaxDelay ?? 0));
                }
            }

            ambienceRecords = records.ToArray();
            ambiencePlaylists = playlists.ToArray();
            ambientAudioSources = new AmbientAudioSource[maxAmbientAudioSources];
            for (var i = 0; i < ambientAudioSources.Length; i++)
                ambientAudioSources[i] = new AmbientAudioSource(gameObject.AddComponent<AudioSource>());
        }

        private void Update()
        {
            switch (currentState)
            {
                case State.Normal:
                    {
                        if (contextChangeQueued)
                        {
                            currentState = State.FadingOut;
                            contextChangeQueued = false;
                        }
                        else
                        {
                            SetAmbientVolume(DaggerfallUnity.Settings.SoundVolume);
                        }
                    }
                    break;
                case State.FadingIn:
                    {
                        if (contextChangeQueued)
                        {
                            fadeInTime = 0f;
                            contextChangeQueued = false;
                            currentState = State.FadingOut;
                        }
                        else if (fadeInTime >= fadeInLength)
                        {
                            fadeInTime = 0f;
                            currentState = State.Normal;
                        }
                        else
                        {
                            fadeInTime += Time.unscaledDeltaTime;
                            SetAmbientVolume(Mathf.Lerp(0f, DaggerfallUnity.Settings.SoundVolume, fadeInTime / fadeInLength));
                        }
                    }
                    break;
                case State.FadingOut:
                    {
                        if (fadeOutTime < fadeOutLength)
                        {
                            fadeOutTime += Time.unscaledDeltaTime;
                            SetAmbientVolume(Mathf.Lerp(DaggerfallUnity.Settings.SoundVolume, 0f, fadeOutTime / fadeOutLength));
                        }
                        else
                        {
                            SetAmbientTracks();
                            fadeOutTime = 0f;
                            currentState = State.FadingIn;
                        }
                    }
                    break;
            }

            for (var i = 0; i < ambientAudioSources.Length; i++)
                ambientAudioSources[i].Step();
        }

        private void OnGUI()
        {
            if (Event.current.type.Equals(EventType.Repaint) && DefaultCommands.showDebugStrings)
            {
                var playing = ambiencePlayer.IsPlaying ? "Playing" : "Stopped";
                var text = $"Dynamic Ambience - {playing} - State: {currentState} - Playlist: {debugPlaylistName} - Track: {debugTrackName}";
                GUI.Label(new Rect(10, 60, 800, 24), text, guiStyle);
                GUI.Label(new Rect(8, 58, 800, 24), text);
            }
        }

        static private void PrintLog(string text)
        {
            Debug.Log($"{modSignature}: {text}");
        }

        private void HandleContextChange()
        {
            PrintLog("Ambience context changed.");
            contextChangeQueued = true;
        }

        private void SetAmbientTracks()
        {
            var isInStartMenu = gameManager.StateManager.CurrentState == StateManager.StateTypes.Start;
            var isNight = daggerfallUnity.WorldTime.Now.IsNight;
            var isInterior = playerEnterExit.IsPlayerInside;
            var isInDungeon = playerEnterExit.IsPlayerInsideDungeon;
            var isInsideDungeonCastle = playerEnterExit.IsPlayerInsideDungeonCastle;
            var locationType = (int)localPlayerGPS.CurrentLocationType;
            var buildingType = (int)playerEnterExit.BuildingType;
            var weatherType = (int)playerWeather.WeatherType;
            var factionId = (int)playerEnterExit.FactionID;
            var climateIndex = localPlayerGPS.CurrentClimateIndex;
            var regionIndex = localPlayerGPS.CurrentRegionIndex;
            var dungeonType = isInDungeon ? (int)playerEnterExit.Dungeon.Summary.DungeonType : (int)DaggerfallConnect.DFRegion.DungeonTypes.NoDungeon;
            var buildingQuality = playerEnterExit.BuildingDiscoveryData.quality;
            var season = (int)gameManager.StreamingWorld.CurrentPlayerLocationObject.CurrentSeason;
            var month = daggerfallUnity.WorldTime.DaggerfallDateTime.MonthOfYear;
            var buildingIsOpen = isInterior && !isInDungeon && 
                PlayerActivate.IsBuildingOpen(playerEnterExit.Interior.BuildingData.BuildingType);

            var audioSourceIndex = 0;
            for (var i = 0; i < ambienceRecords.Length; i++)
            {
                var record = ambienceRecords[i];
                if (record.StartMenu.HasValue && record.StartMenu != isInStartMenu)
                    continue;
                if (record.Night.HasValue && record.Night != isNight)
                    continue;
                if (record.Interior.HasValue && record.Interior != isInterior)
                    continue;
                if (record.Dungeon.HasValue && record.Dungeon != isInDungeon)
                    continue;
                if (record.DungeonCastle.HasValue && record.DungeonCastle != isInsideDungeonCastle)
                    continue;
                if (record.LocationType != null && !record.LocationType.Contains(locationType))
                    continue;
                if (record.BuildingType != null && !record.BuildingType.Contains(buildingType))
                    continue;
                if (record.WeatherType != null && !record.WeatherType.Contains(weatherType))
                    continue;
                if (record.FactionId != null && !record.FactionId.Contains(factionId))
                    continue;
                if (record.ClimateIndex != null && !record.ClimateIndex.Contains(climateIndex))
                    continue;
                if (record.RegionIndex != null && !record.RegionIndex.Contains(regionIndex))
                    continue;
                if (record.DungeonType != null && !record.DungeonType.Contains(dungeonType))
                    continue;
                if (record.BuildingQuality != null && !record.BuildingQuality.Contains(buildingQuality))
                    continue;
                if (record.Season != null && !record.Season.Contains(season))
                    continue;
                if (record.Month != null && !record.Month.Contains(month))
                    continue;
                if (record.BuildingIsOpen.HasValue && record.BuildingIsOpen != buildingIsOpen)
                    continue;
                ambientAudioSources[audioSourceIndex++].SetPlaylist(ambiencePlaylists[i]);
                audioSourceIndex %= maxAmbientAudioSources;
            }
        }

        private void GetDebuggingText(string track, out string playlistName, out string songName)
        {
            playlistName = $"{Path.GetFileName(Path.GetDirectoryName(track))}";
            songName = Path.GetFileName(track);
        }

        private void SetAmbientVolume(float value)
        {
            for (var i = 0; i < ambientAudioSources.Length; i++)
                ambientAudioSources[i].AudioSource.volume = value;
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

        private void OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleContextChange();
        }

        private void OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleContextChange();
        }

        private void OnTransitionDungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleContextChange();
        }

        private void OnTransitionDungeonExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleContextChange();
        }

        private void OnMapPixelChanged(DFPosition mapPixel)
        {
            HandleContextChange();
        }

        private void OnEnterLocationRect(DFLocation location)
        {
            HandleContextChange();
        }

        private void OnDuskEvent()
        {
            HandleContextChange();
        }

        private void OnDawnEvent()
        {
            HandleContextChange();
        }

        private static void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            Instance.HandleContextChange();
            Instance.isInCombat = false;
            Instance.gameLoaded = true;
        }

        private static void StartGameBehaviour_OnStartGame(object sender, EventArgs e)
        {
            Instance.HandleContextChange();
            Instance.gameLoaded = true;
        }

        private void OnDeath(DaggerfallEntity entity)
        {
            // Fade out on death.
            currentState = State.FadingOut;
            currentPlaylist = -1;
            isInCombat = false;
            gameLoaded = false;
        }
    }
}
