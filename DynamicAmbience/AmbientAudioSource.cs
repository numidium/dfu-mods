using System.Collections;
using System.IO;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace DynamicAmbience
{
    public sealed class AmbientAudioSource : MonoBehaviour
    {
        private enum State : byte
        {
            Idle,
            Playing,
            FadingOut,
            FadingIn
        }

        private const float fadeOutLength = 2f;
        private const float fadeInLength = 2f;
        private State currentState;
        private bool clipFileLoaded;
        private bool hasPlayed;
        private float timeSinceLastPlay;
        private float delay;
        private bool playlistChangeQueued;
        private float fadeTime;
        private AudioClip oldClip;
        private Playlist queuedPlaylist; 
        public AudioSource AudioSource { get; private set; }
        public bool IsReady => clipFileLoaded && AudioSource.clip != null && AudioSource.clip.loadState == AudioDataLoadState.Loaded;
        public bool IsFadingOut => currentState == State.FadingOut;

        public Playlist Playlist { get; private set; }

        public void QueuePlaylist(Playlist playlist_)
        {
            var hasPlaylist = playlist_ != null;
            if (hasPlaylist)
                delay = Random.Range(playlist_.MinDelay, playlist_.MaxDelay);
            if (currentState != State.Idle)
            {
                queuedPlaylist = playlist_;
                playlistChangeQueued = true; 
            }
            else if (hasPlaylist)
            {
                Playlist = playlist_;
                currentState = State.FadingIn;
                StartNextTrack();
            }
        }

        public void StopAndUnload()
        {
            if (!AudioSource.isPlaying)
                return;
            AudioSource.Stop();
            oldClip = AudioSource.clip;
            DeleteClip(oldClip);
        }

        private void StartNextTrack()
        {
            clipFileLoaded = false;
            hasPlayed = false;
            if (Playlist != null)
                StartCoroutine(StartClip(Playlist.GetNextTrack()));
        }

        private IEnumerator StartClip(string path)
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip($"file://{path}", AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.responseCode != 200)
                {   
                    AudioSource.clip = null;
                    Logger.PrintLog($"Could not load sound file: {path}");
                }
                else
                {
                    if (AudioSource.clip)
                    {
                        oldClip = AudioSource.clip;
                        DeleteClip(oldClip);
                    }

                    AudioSource.clip = DownloadHandlerAudioClip.GetContent(request);
                    if (AudioSource.clip.loadState == AudioDataLoadState.Loaded)
                    {
                        clipFileLoaded = true;
                        AudioSource.clip.name = Path.GetFileNameWithoutExtension(path);
                    }
                    else
                        Logger.PrintLog($"Possible invalid sound format in file: {path}");
                }
            }
        }

        private void DeleteClip(AudioClip clip)
        {
            if (clip.UnloadAudioData())
                Destroy(clip);
            else 
                Logger.PrintLog("Failed to unload audio clip.");
        }

        private float VolumeLevel => DynamicAmbienceSettings.Instance.VolumeLevel * DaggerfallUnity.Settings.SoundVolume;

        private void Awake()
        {
            AudioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            if (Playlist == null)
                return;
            timeSinceLastPlay += Time.unscaledDeltaTime;
            if (IsReady && AudioSource.clip.loadState == AudioDataLoadState.Loaded && !AudioSource.isPlaying && timeSinceLastPlay >= delay)
            {
                if (!hasPlayed || Playlist.TrackCount < 2)
                {
                    if (Playlist.IsPositional)
                       AudioSource.PlayClipAtPoint(AudioSource.clip, GameManager.Instance.PlayerObject.transform.position); 
                    else
                        AudioSource.Play();
                    timeSinceLastPlay = 0f;
                    hasPlayed = true;
                }
                else
                    StartNextTrack();
                if (Playlist.MaxDelay > 0f)
                    delay = Random.Range(Playlist.MinDelay, Playlist.MaxDelay);
                else
                    delay = Playlist.MinDelay;
            }
            
            switch (currentState)
            {
                case State.Playing:
                    {
                        if (playlistChangeQueued)
                        {
                            currentState = State.FadingOut;
                            playlistChangeQueued = false;
                        }
                        else
                        {
                            AudioSource.volume = VolumeLevel;
                        }
                    }
                    break;
                case State.FadingIn:
                    {
                        if (playlistChangeQueued)
                        {
                            fadeTime = 0f;
                            playlistChangeQueued = false;
                            currentState = State.FadingOut;
                        }
                        else if (fadeTime >= fadeInLength)
                        {
                            fadeTime = 0f;
                            currentState = State.Playing;
                        }
                        else
                        {
                            fadeTime += Time.unscaledDeltaTime;
                            AudioSource.volume = Mathf.Lerp(0f, VolumeLevel, fadeTime / fadeInLength);
                        }
                    }
                    break;
                case State.FadingOut:
                    {
                        if (fadeTime < fadeOutLength)
                        {
                            fadeTime += Time.unscaledDeltaTime;
                            AudioSource.volume = Mathf.Lerp(VolumeLevel, 0f, fadeTime / fadeOutLength);
                        }
                        else
                        {
                            StopAndUnload();
                            Playlist = queuedPlaylist;
                            queuedPlaylist = null;
                            if (Playlist != null)
                            {
                                StartNextTrack();
                                currentState = State.FadingIn;
                            }
                            else
                                currentState = State.Idle;
                            fadeTime = 0f;
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}