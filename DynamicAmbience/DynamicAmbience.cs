using DaggerfallConnect;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Utility;
using FullSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DynamicAmbience
{
    public sealed class DynamicAmbience : MonoBehaviour
    {
        private enum LoggingLevel
        {
            None,
            Minimal,
            Verbose
        }

        public static DynamicAmbience Instance { get; private set; }
        private static Mod mod;
        private LoggingLevel loggingLevel;
        private DaggerfallUnity daggerfallUnity;
        private PlayerGPS localPlayerGPS;
        private PlayerEnterExit playerEnterExit;
        private PlayerWeather playerWeather;
        private GameManager gameManager;
        private PlayerEntity playerEntity;
        private AmbienceRecord[] ambienceRecords;
        private Playlist[] ambiencePlaylists;
        private AmbientAudioSource[] ambientAudioSources;
        private const int maxAmbientAudioSources = 10;
        private bool isInCombat;
        private bool lastSwimmingState;
        private bool lastSubmergedState;
        private bool lastInsideCastle;
        private GUIStyle guiStyle;
        private const string soundDirectory = "Sound";
        private const string baseDirectory = "DynAmbience";
        private const string playlistsDirectory = "Playlists";
        private const string playlistSearchPattern = "*.json";
        private const string soundSearchPattern = "*.ogg";
        private string basePath;
        private string playlistsPath;
        private delegate void StateChangeEvent();
        private event StateChangeEvent OnStartSwimming;
        private event StateChangeEvent OnStopSwimming;
        private event StateChangeEvent OnSubmerge;
        private event StateChangeEvent OnEmerge;
        private event StateChangeEvent OnMoveToCastleBlock;
        private event StateChangeEvent OnMoveFromCastleBlock;

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
            switch (settings.GetValue<int>("Options", "Logging Level"))
            {
                case (int)LoggingLevel.Minimal:
                    loggingLevel = LoggingLevel.Minimal;
                    break;
                case (int)LoggingLevel.Verbose:
                    loggingLevel = LoggingLevel.Verbose;
                    break;
                default:
                    break;
            }
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

            // Setup events.
            StartGameBehaviour.OnStartGame += OnStartGame;
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            PlayerGPS.OnMapPixelChanged += OnMapPixelChanged;
            PlayerGPS.OnEnterLocationRect += OnEnterLocationRect;
            PlayerGPS.OnExitLocationRect += OnExitLocationRect;
            WorldTime.OnNewHour += OnNewHourEvent;
            OnStartSwimming += OnStartSwimmingEvent;
            OnStopSwimming += OnStopSwimmingEvent;
            OnSubmerge += OnSubmergeEvent;
            OnEmerge += OnEmergeEvent;
            OnMoveToCastleBlock += OnMoveToCastleBlockEvent;
            OnMoveFromCastleBlock += OnMoveFromCastleBlockEvent;
            playerEntity.OnDeath += OnDeathEvent;
            SaveLoadManager.OnLoad += OnLoadEvent;
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
                    playlists.Add(new Playlist(Path.GetFileName(playlistPath), trackFileNames, entry.MinDelay ?? 0, entry.MaxDelay ?? 0, entry.Positional ?? false));
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
            // Custom state transition events
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

            var isPlayerSubmerged = playerEnterExit.IsPlayerSubmerged;
            if (isPlayerSubmerged && !lastSubmergedState)
            {
                lastSubmergedState = true;
                OnSubmerge(); 
            }
            else if (!isPlayerSubmerged && lastSubmergedState)
            {
                lastSubmergedState = false;
                OnEmerge();
            }

            if (playerEnterExit.IsPlayerInsideDungeon)
            {
                var isInsideDungeonCastle = playerEnterExit.IsPlayerInsideDungeonCastle;
                if (isInsideDungeonCastle && !lastInsideCastle)
                {
                    lastInsideCastle = false;
                    OnMoveToCastleBlock();
                }
                else if (!isInsideDungeonCastle && lastInsideCastle)
                {
                    lastInsideCastle = true;
                    OnMoveFromCastleBlock();
                }
            }
        }

        private void HandleContextChange(bool isLeavingRect = false, [CallerMemberName] string eventName = null)
        {
            if (loggingLevel == LoggingLevel.Verbose)
                Logger.PrintLog(eventName);
            SetAmbientTracks(isLeavingRect);
        }

        private void SetAmbientTracks(bool isLeavingRect)
        {
            var isInStartMenu = gameManager.StateManager.CurrentState == StateManager.StateTypes.Start;
            var isNight = daggerfallUnity.WorldTime.Now.IsNight;
            var isInterior = playerEnterExit.IsPlayerInside;
            var isInDungeon = playerEnterExit.IsPlayerInsideDungeon;
            var isInsideDungeonCastle = lastInsideCastle = playerEnterExit.IsPlayerInsideDungeonCastle;
            var locationType = !isLeavingRect && localPlayerGPS.IsPlayerInLocationRect ? (int)localPlayerGPS.CurrentLocationType : -1;
            var buildingType = (int)playerEnterExit.BuildingType;
            var weatherType = (int)playerWeather.WeatherType;
            var factionId = (int)playerEnterExit.FactionID;
            var climateIndex = localPlayerGPS.CurrentClimateIndex;
            var regionIndex = localPlayerGPS.CurrentRegionIndex;
            var dungeonType = isInDungeon ? (int)playerEnterExit.Dungeon.Summary.DungeonType : (int)DFRegion.DungeonTypes.NoDungeon;
            var buildingQuality = playerEnterExit.BuildingDiscoveryData.quality;
            var season = (int)daggerfallUnity.WorldTime.Now.SeasonValue;
            var month = daggerfallUnity.WorldTime.DaggerfallDateTime.MonthOfYear;
            var isShop = isInterior && RMBLayout.IsShop(playerEnterExit.Interior.BuildingData.BuildingType);
            var buildingIsOpen = isInterior && !isInDungeon && isShop && PlayerActivate.IsBuildingOpen(playerEnterExit.Interior.BuildingData.BuildingType);
            var isSwimming = playerEnterExit.IsPlayerSwimming;
            var isSubmerged = playerEnterExit.IsPlayerSubmerged;

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
                if (record.BuildingIsOpen.HasValue && isInterior && ((isShop && record.BuildingIsOpen.Value != buildingIsOpen) || !isShop))
                    continue;
                if (record.Swimming.HasValue && record.Swimming.Value != isSwimming)
                    continue;
                if (record.Submerged.HasValue && record.Submerged.Value != isSubmerged)
                    continue;
                activePlaylists[activePlaylistCount++] = ambiencePlaylists[i];
            }

            for (var i = 0; i < ambientAudioSources.Length; i++)
            {
                var source = ambientAudioSources[i];
                if (source.Playlist == null)
                    continue;
                if (!activePlaylists.Contains(source.Playlist))
                {
                    source.QueuePlaylist(null);
                    if (loggingLevel >= LoggingLevel.Minimal)
                        Logger.PrintLog($"Removing ambience player: {source.Playlist.Name}");
                }
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
                        if (loggingLevel >= LoggingLevel.Minimal)
                            Logger.PrintLog($"Adding ambience player: {activePlaylists[i].Name}");
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
        private void OnExitLocationRect()
        {
            HandleContextChange(true);
        }

        private void OnNewHourEvent()
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

        private void OnSubmergeEvent()
        {
            HandleContextChange();
        }

        private void OnEmergeEvent()
        {
            HandleContextChange();
        }

        private void OnMoveToCastleBlockEvent()
        {
            HandleContextChange();
        }

        private void OnMoveFromCastleBlockEvent()
        {
            HandleContextChange();
        }

        private static void OnStartGame(object sender, EventArgs e)
        {
            //Instance.HandleContextChange();
        }

        private void OnDeathEvent(DaggerfallEntity entity)
        {
            isInCombat = false;
        }

        private void OnLoadEvent(SaveData_v1 data)
        {
            HandleContextChange();
        }
    }
}
