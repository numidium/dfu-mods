using DaggerfallConnect;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using FullSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;

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

        public static DynamicAmbience Instance { get; private set; }
        private static Mod mod;
        private DaggerfallUnity daggerfallUnity;
        private PlayerGPS localPlayerGPS;
        private PlayerEnterExit playerEnterExit;
        private PlayerWeather playerWeather;
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
        private bool lastSwimmingState;
        private bool resumeIsEnabled = true;
        private int currentPlaylist;
        private State currentState;
        private string debugPlaylistName;
        private string debugTrackName;
        private GUIStyle guiStyle;
        private const string soundDirectory = "Sound";
        private const string baseDirectory = "DynAmbience";
        private const string playlistsDirectory = "Playlists";
        private const string playlistSearchPattern = "*.json";
        private const string soundSearchPattern = "*.ogg";
        private string basePath;
        private string playlistsPath;
        private delegate void SwimmingEvent();
        private event SwimmingEvent OnStartSwimming;
        private event SwimmingEvent OnStopSwimming;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<DynamicAmbience>();
            DontDestroyOnLoad(go);
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
            currentState = State.FadingIn;
            fadeInTime = 0f;
            currentPlaylist = -1;

            // Setup events.
            StartGameBehaviour.OnStartGame += OnStartGame;
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            PlayerGPS.OnMapPixelChanged += OnMapPixelChanged;
            PlayerGPS.OnEnterLocationRect += OnEnterLocationRect;
            WorldTime.OnDusk += OnDuskEvent;
            WorldTime.OnDawn += OnDawnEvent;
            OnStartSwimming += OnStartSwimmingEvent;
            OnStopSwimming += OnStopSwimmingEvent;
            playerEntity.OnDeath += OnDeath;
            guiStyle = new GUIStyle();
            guiStyle.normal.textColor = Color.black;
            Logger.PrintInitMessage();
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
                        Logger.PrintLog($"Playlist in {playlistFileName} has no name.");
                        continue;
                    }

                    var playlistPath = Path.Combine(basePath, entry.Name);
                    if (!Directory.Exists(playlistPath))
                    {
                        Logger.PrintLog($"Could not find directory for playlist: {entry.Name} referenced in file {playlistFileName}.");
                        continue;   
                    }

                    var trackFileNames = Directory.GetFiles(playlistPath, soundSearchPattern);
                    if (trackFileNames.Length == 0)
                    {
                        Logger.PrintLog($"Playlist directory {entry.Name} in file {playlistFileName} is empty. Skipping...");
                        continue;
                    }

                    records.Add(entry);
                    playlists.Add(new Playlist(trackFileNames, entry.MinDelay ?? 0, entry.MaxDelay ?? 0, entry.Positional ?? false));
                }
            }

            ambienceRecords = records.ToArray();
            ambiencePlaylists = playlists.ToArray();
            ambientAudioSources = new AmbientAudioSource[maxAmbientAudioSources];
            for (var i = 0; i < ambientAudioSources.Length; i++)
                ambientAudioSources[i] = gameObject.AddComponent<AmbientAudioSource>();
        }

        private void Update()
        {
            var isSwimming = playerEnterExit.IsPlayerSwimming;
            if (isSwimming && !lastSwimmingState)
            {
                lastSwimmingState = true;
                OnStartSwimming();
            }
            else if (!isSwimming && lastSwimmingState)
            {
                lastSwimmingState = false;
                OnStopSwimming();
            }
        }

        private void HandleContextChange()
        {
            Logger.PrintLog("Ambience context changed.");
            SetAmbientTracks();
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
            var dungeonType = isInDungeon ? (int)playerEnterExit.Dungeon.Summary.DungeonType : (int)DFRegion.DungeonTypes.NoDungeon;
            var buildingQuality = playerEnterExit.BuildingDiscoveryData.quality;
            var season = (int)gameManager.StreamingWorld.CurrentPlayerLocationObject.CurrentSeason;
            var month = daggerfallUnity.WorldTime.DaggerfallDateTime.MonthOfYear;
            var buildingIsOpen = isInterior && !isInDungeon && (int)playerEnterExit.Interior.BuildingData.BuildingType < 18 && 
                PlayerActivate.IsBuildingOpen(playerEnterExit.Interior.BuildingData.BuildingType);

            var activePlaylists = new Playlist[ambienceRecords.Length];
            var activePlaylistCount = 0;
            for (var i = 0; i < ambienceRecords.Length; i++)
            {
                var record = ambienceRecords[i];
                if (record.StartMenu.HasValue && record.StartMenu.Value != isInStartMenu)
                    continue;
                if (record.Night.HasValue && record.Night.Value != isNight)
                    continue;
                if (record.Interior.HasValue && record.Interior.Value != isInterior)
                    continue;
                if (record.Dungeon.HasValue && record.Dungeon.Value != isInDungeon)
                    continue;
                if (record.DungeonCastle.HasValue && record.DungeonCastle.Value != isInsideDungeonCastle)
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
                if (record.BuildingIsOpen.HasValue && record.BuildingIsOpen.Value != buildingIsOpen)
                    continue;
                activePlaylists[activePlaylistCount++] = ambiencePlaylists[i];
            }

            for (var i = 0; i < ambientAudioSources.Length; i++)
            {
                var source = ambientAudioSources[i];
                if (source.Playlist == null)
                    continue;
                if (!activePlaylists.Contains(source.Playlist))
                    source.QueuePlaylist(null);
            }

            for (var i = 0; i < activePlaylists.Length && activePlaylists[i] != null; i++)
            {
                if (SourcesHavePlaylist(activePlaylists[i]))
                    continue;
                for (var j = 0; j < ambientAudioSources.Length; j++)
                {
                    if (ambientAudioSources[j].Playlist == null)
                    {
                        ambientAudioSources[j].QueuePlaylist(activePlaylists[i]);
                        break;
                    }
                }
            }
        }

        private void GetDebuggingText(string track, out string playlistName, out string songName)
        {
            playlistName = $"{Path.GetFileName(Path.GetDirectoryName(track))}";
            songName = Path.GetFileName(track);
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

        private bool SourcesHavePlaylist(Playlist playlist)
        {
            for (var i = 0; i < ambientAudioSources.Length; i++)
            {
                if (ambientAudioSources[i].Playlist == playlist)
                    return true;
            }

            return false;
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

        private void OnStartSwimmingEvent()
        {
            HandleContextChange();
        }

        private void OnStopSwimmingEvent()
        {
            HandleContextChange();
        }

        private static void OnStartGame(object sender, EventArgs e)
        {
            //Instance.HandleContextChange();
        }

        private void OnDeath(DaggerfallEntity entity)
        {
            // Fade out on death.
            currentState = State.FadingOut;
            currentPlaylist = -1;
            isInCombat = false;
        }
    }
}
