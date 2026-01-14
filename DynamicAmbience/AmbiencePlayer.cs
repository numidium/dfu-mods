// Adapted from SongPlayer.cs.
// Original Author: Interkarma.
using UnityEngine;
using System;
using System.IO;
using DaggerfallWorkshop;

namespace DynamicAmbience
{
    [RequireComponent(typeof(AudioSource))]
    public class AmbiencePlayer : MonoBehaviour
    {
        public string ModSignature { private get; set; }
        public AudioSource AudioSource { get; private set; }
        public bool IsPlaying => AudioSource.isPlaying || (AudioSource.clip && AudioSource.clip.loadState == AudioDataLoadState.Loading);
        public bool IsStoppedClip => clipStarted && AudioSource.clip && AudioSource.clip.loadState == AudioDataLoadState.Loaded && !AudioSource.isPlaying;
        public float CurrentSecond => AudioSource.time;
        private bool clipStarted;
        private AudioClip streamedTrack;
        private AudioClip oldSong;

        void Start()
        {
            AudioSource = GetComponent<AudioSource>();
            AudioSource.volume = 0f;
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        [System.Security.SecurityCritical]
        void Update()
        {
            if (!IsPlaying)
            {
                if (AudioSource.clip)
                    AudioSource.Play();

                clipStarted = true;
                if (oldSong)
                {
                    oldSong.UnloadAudioData();
                    try
                    {
                        Destroy(oldSong);
                    }
                    catch (AccessViolationException)
                    {
                        Debug.Log($"{ModSignature}: Caught/ignored access violation when destroying old audio clip.");
                    }
                }
            }
        }

        public void Play(string track, float timeSeek = 0f)
        {
            Stop();
            oldSong = streamedTrack;
            if (TryLoadTrack(track, out streamedTrack))
            {
                AudioSource.clip = streamedTrack;
                AudioSource.time = timeSeek;
                AudioSource.loop = false;
                clipStarted = false;
            }
        }

        public void Stop()
        {
            // Reset audiosource clip
            AudioSource.Stop();
            AudioSource.clip = null;
        }

        private bool TryLoadTrack(string path, out AudioClip audioClip)
        {
            if (File.Exists(path))
            {
                var www = new WWW("file://" + path); // the "non-deprecated" class gives me compiler errors so it can suck it
                audioClip = www.GetAudioClip(true, true);
                return audioClip != null;
            }

            audioClip = null;
            return false;
        }

        private void LogErrorMessage(string errorMessage)
        {
            DaggerfallUnity.LogMessage($"{nameof(AmbiencePlayer)}: {errorMessage}");
        }
    }
}