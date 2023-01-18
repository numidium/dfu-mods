using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DynamicMusic
{
    public sealed class DynamicMusic : MonoBehaviour
    {
        public static DynamicMusic Instance { get; private set; }
        private static Mod mod;
        private SongManager songManager;
        private DaggerfallSongPlayer songPlayer;
        private GameManager gameManager;
        private float stateCheckInterval;
        private float stateCheckDelta;
        private byte taperOffLength;
        private byte taperOff;
        private SongFiles[] combatPlaylist;
        private byte playlistIndex;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<DynamicMusic>();
            //mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        private void Awake()
        {
            //var settings = mod.GetSettings();
            //LoadSettings(settings, new ModSettingsChange());
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
            stateCheckInterval = 3f;
            taperOffLength = 2;
            combatPlaylist = new SongFiles[6]
            {
                SongFiles.song_17, // fighters trainers
                SongFiles.song_30, // unused sneaking (?) theme
                SongFiles.song_d1,
                SongFiles.song_d2,
                SongFiles.song_d3,
                SongFiles.song_d4
            };

            playlistIndex = (byte)UnityEngine.Random.Range(0, combatPlaylist.Length);
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
        }

        private void Update()
        {
            stateCheckDelta += Time.deltaTime;
            // Only perform state check once per assigned interval.
            if (stateCheckDelta < stateCheckInterval)
                return;
            if (gameManager.AreEnemiesNearby(resting: true))
            {
                if (taperOff == 0)
                {
                    songPlayer.Play(combatPlaylist[playlistIndex]);
                    playlistIndex += (byte)UnityEngine.Random.Range(1, combatPlaylist.Length - 1);
                    playlistIndex %= (byte)combatPlaylist.Length;
                }

                taperOff = taperOffLength;
            }
            else if (taperOff > 0 && --taperOff == 0)
            {
                songManager.StartPlaying(); // Return control of music to song manager.
                songPlayer.AudioSource.loop = true;
            }

            stateCheckDelta = 0f;
        }

        // Load Interior/Exterior song player when player moves to either
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

        private void LoadSongManager()
        {
            var go = GameObject.Find("SongPlayer");
            songManager = go.GetComponent<SongManager>();
            songPlayer = songManager.SongPlayer;
            taperOff = 0; // Don't continue tapering if we have a freshly loaded player.
            var song = songManager.SongPlayer.Song;
            for (int i = 0; i < combatPlaylist.Length; i++)
            {
                if (song == combatPlaylist[i])
                {
                    songManager.StartPlaying();
                    break;
                }
            }
        }
    }
}
