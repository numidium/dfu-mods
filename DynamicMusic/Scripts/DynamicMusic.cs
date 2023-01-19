using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DynamicMusic
{
    [RequireComponent(typeof(DaggerfallSongPlayer))]
    public sealed class DynamicMusic : MonoBehaviour
    {
        public static DynamicMusic Instance { get; private set; }
        private static Mod mod;
        private SongManager songManager;
        private DaggerfallSongPlayer combatSongPlayer;
        private GameManager gameManager;
        private float stateCheckInterval;
        private float stateCheckDelta;
        private float fadeLength;
        private float fadeTime;
        private byte taperOffLength;
        private byte taperOff;
        private SongFiles[] defaultSongs;
        private List<string> combatPlaylist;
        private byte playlistIndex;
        private string musicPath;
        private bool combatMusicIsMidi;

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
            combatSongPlayer = GetComponent<DaggerfallSongPlayer>();
            musicPath = Path.Combine(Application.streamingAssetsPath, "Sound", "DynMusic_Combat");
            var fileNames = Directory.GetFiles(musicPath, "*.ogg");
            combatPlaylist = new List<string>(fileNames.Length);
            foreach (var fileName in fileNames)
                combatPlaylist.Add(fileName);
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
            fadeLength = 1f;
            defaultSongs = new SongFiles[]
            {
                SongFiles.song_17, // fighters trainers
                SongFiles.song_30  // unused sneaking (?) theme
            };

            playlistIndex = (byte)UnityEngine.Random.Range(0, combatPlaylist.Count);
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            gameManager.PlayerEntity.OnDeath += OnDeath;
        }

        private void Update()
        {
            stateCheckDelta += Time.deltaTime;
            if (fadeTime > 0f)
            {
                // Fade out combat music during taper time.
                fadeTime += Time.deltaTime;
                combatSongPlayer.AudioSource.volume = Mathf.Lerp(DaggerfallUnity.Settings.MusicVolume, 0f, fadeTime / fadeLength);
                // End fade when time elapsed.
                if (fadeTime >= fadeLength)
                    StopCombatMusic();
            }
            else if (combatMusicIsMidi && !combatSongPlayer.IsPlaying) // loop combat midi
                combatSongPlayer.Play();

            // Only perform state check once per assigned interval.
            if (stateCheckDelta < stateCheckInterval)
                return;
            if (gameManager.AreEnemiesNearby(resting: true))
            {
                if (taperOff == 0 || !IsCombatMusicPlaying())
                {
                    songManager.StopPlaying();
                    songManager.enabled = false;
                    songManager.SongPlayer.enabled = false;
                    combatSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                    combatSongPlayer.AudioSource.loop = true;
                    int playlistCount;
                    if (combatPlaylist.Count > 0 && TryLoadSong(musicPath, combatPlaylist[playlistIndex], out var song))
                    {
                        combatSongPlayer.AudioSource.clip = song;
                        combatSongPlayer.AudioSource.Play();
                        playlistCount = combatPlaylist.Count;
                        combatMusicIsMidi = false;
                    }
                    else
                    {
                        var songFile = defaultSongs[playlistIndex % defaultSongs.Length];
                        combatSongPlayer.Play(songFile);
                        combatSongPlayer.Song = songFile;
                        playlistCount = defaultSongs.Length;
                        combatMusicIsMidi = combatSongPlayer.AudioSource.clip == null;
                    }

                    combatSongPlayer.enabled = true;
                    playlistIndex += (byte)UnityEngine.Random.Range(1, playlistCount - 1);
                    playlistIndex %= (byte)playlistCount;
                }

                taperOff = taperOffLength;
            }
            else if (taperOff > 0 && --taperOff == 0)
            {
                // Re-enable vanilla music system.
                songManager.SongPlayer.enabled = true;
                songManager.enabled = true;
                songManager.StartPlaying();
            }
            else if (taperOff <= (byte)fadeLength && combatSongPlayer.AudioSource.isPlaying || (combatMusicIsMidi && combatSongPlayer.IsPlaying))
            {
                // Begin fade.
                fadeTime = Time.deltaTime;
            }

            stateCheckDelta = 0f;
        }

        private void LoadSongManager()
        {
            var go = GameObject.Find("SongPlayer");
            songManager = go.GetComponent<SongManager>();
            taperOff = 0; // Don't continue tapering if we have a freshly loaded player.
            songManager.SongPlayer.enabled = true;
            songManager.enabled = true;
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
            fadeTime = 0f;
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

        // Could play a sting here.
        private void OnDeath(DaggerfallWorkshop.Game.Entity.DaggerfallEntity entity)
        {
            StopCombatMusic();
        }
    }
}
