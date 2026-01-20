using System.Collections;
using System.IO;
using DaggerfallWorkshop.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace DynamicAmbience
{
    public sealed class AmbientAudioSource : MonoBehaviour
    {
        public AudioSource AudioSource { get; private set; }
        private bool clipFileLoaded;
        private bool hasPlayed;
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

        public void SetPlaylist(Playlist playlist_)
        {
            playlist = playlist_;
            delay = Random.Range(playlist.MinDelay, playlist.MaxDelay);
            StartNextTrack();
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
            StartCoroutine(StartClip(playlist.GetNextTrack()));
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

        private void Awake()
        {
            AudioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            if (playlist == null)
                return;
            timeSinceLastPlay += Time.unscaledDeltaTime;
            if (IsReady && AudioSource.clip.loadState == AudioDataLoadState.Loaded && !AudioSource.isPlaying && timeSinceLastPlay >= delay)
            {
                if (!hasPlayed || playlist.TrackCount < 2)
                {
                    if (playlist.IsPositional)
                       AudioSource.PlayClipAtPoint(AudioSource.clip, GameManager.Instance.PlayerObject.transform.position); 
                    else
                        AudioSource.Play();
                    timeSinceLastPlay = 0f;
                    hasPlayed = true;
                }
                else
                    StartNextTrack();
                if (playlist.MaxDelay > 0f)
                    delay = Random.Range(playlist.MinDelay, playlist.MaxDelay);
                else
                    delay = playlist.MinDelay;
            }
        }
    }
}