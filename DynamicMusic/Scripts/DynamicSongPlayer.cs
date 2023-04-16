// Adapted from SongPlayer.cs.
// Original Author: Interkarma.
using UnityEngine;
using System;
using System.IO;
using DaggerfallWorkshop.AudioSynthesis.Sequencer;
using DaggerfallWorkshop.AudioSynthesis.Synthesis;
using DaggerfallWorkshop.AudioSynthesis.Midi;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop;

namespace DynamicMusic
{
    [RequireComponent(typeof(AudioSource))]
    public class DynamicSongPlayer : MonoBehaviour
    {
        const string sourceFolderName = "SoundFonts";
        const string defaultSoundFontFilename = "TimGM6mb.sf2";
        const int sampleRate = 48000;
        const int polyphony = 100;

        [NonSerialized, HideInInspector]
        public bool IsSequencerPlaying = false;

        public bool ShowDebugString = false;

        [Range(0.0f, 10.0f)]
        public float Gain = 5.0f;
        public string SongFolder = "Songs/";
        public SongFiles Song = SongFiles.song_none;
        public AudioSource AudioSource { get; private set; }
        public bool IsImported { get; set; }
        public bool IsAudioSourcePlaying => AudioSource.isPlaying || (AudioSource.clip && AudioSource.clip.loadState == AudioDataLoadState.Loading);
        public bool IsPlaying => IsAudioSourcePlaying || (midiSequencer != null && midiSequencer.IsPlaying);
        public bool IsStoppedClip => clipStarted && AudioSource.clip && AudioSource.clip.loadState == AudioDataLoadState.Loaded && !AudioSource.isPlaying;
        Synthesizer midiSynthesizer = null;
        MidiFileSequencer midiSequencer = null;
        float[] sampleBuffer = new float[0];
        int channels = 0;
        int bufferLength = 0;
        int numBuffers = 0;
        bool playEnabled = false;
        float oldGain;
        bool clipStarted;

        void Start()
        {
            AudioSource = GetComponent<AudioSource>();
            InitSynth();

            DaggerfallVidPlayerWindow.OnVideoStart += DaggerfallVidPlayerWindow_OnVideoStart;
            DaggerfallVidPlayerWindow.OnVideoEnd += DaggerfallVidPlayerWindow_OnVideoEnd;
        }

        void Update()
        {
            // MIDI
            if (!IsImported)
            {
                if (midiSequencer != null)
                {
                    IsSequencerPlaying = midiSequencer.IsPlaying;
                    Gain = (AudioSource.volume * 5f);
                    if (Song != SongFiles.song_none && !midiSequencer.IsPlaying)
                        PlaySequencer(Song);
                }
            }
            // Non-MIDI
            else
            {
                if (!IsAudioSourcePlaying)
                {
                    StopSequencer();
                    AudioSource.Play();
                    clipStarted = true;
                }
            }
        }

        public void Play()
        {
            if (Song == SongFiles.song_none)
                return;

            Play(Song);
        }

        /// <summary>
        /// Play current song.
        /// </summary>
        public void Play(SongFiles song)
        {
            if (!InitSynth())
                return;
            // Stop if playing another song
            Stop();
            // Import custom song
            if (IsImported = SoundReplacement.TryImportSong(song, out var clip))
            {
                Song = song;
                AudioSource.clip = clip;
                clipStarted = false;
            }
            else
                PlaySequencer(song);
            AudioSource.loop = true;
        }

        public void Play(string track)
        {
            Stop();
            if (IsImported = TryLoadSong(track, out var song))
            {
                AudioSource.clip = song;
                AudioSource.loop = false;
                clipStarted = false;
            }
        }

        /// <summary>
        /// Stop playing song.
        /// </summary>
        public void Stop()
        {
            if (!InitSynth())
                return;
            // Reset audiosource clip
            if (IsImported)
            {
                IsImported = false;
                AudioSource.Stop();
                AudioSource.clip = null;
                AudioSource.Play();
            }

            Song = SongFiles.song_none;
            StopSequencer();
        }

        public void StopSequencer()
        {
            // Stop if playing a song
            if (midiSequencer.IsPlaying)
            {
                midiSequencer.Stop();
                midiSynthesizer.NoteOffAll(true);
                midiSynthesizer.ResetSynthControls();
                midiSynthesizer.ResetPrograms();
                playEnabled = false;
            }
        }

        private bool InitSynth()
        {
            if (AudioSource == null)
            {
                DaggerfallUnity.LogMessage("DynamicSongPlayer: Could not find AudioSource component.");
                return false;
            }

            // Create synthesizer and load bank
            if (midiSynthesizer == null)
            {
                // Get number of channels
                if (AudioSettings.driverCapabilities.ToString() == "Mono")
                    channels = 1;
                else
                    channels = 2;

                // Create synth
                AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);
                midiSynthesizer = new Synthesizer(sampleRate, channels, bufferLength / numBuffers, numBuffers, polyphony);

                // Load bank data
                string filename = DaggerfallUnity.Settings.SoundFont;
                byte[] bankData = LoadBank(filename);
                if (bankData == null)
                {
                    // Attempt to fallback to default internal soundfont
                    bankData = LoadDefaultSoundFont();
                    filename = defaultSoundFontFilename;
                    Debug.LogFormat("Using default SoundFont {0}", defaultSoundFontFilename);
                }
                else
                {
                    Debug.LogFormat("Trying custom SoundFont {0}", filename);
                }

                // Assign to synth
                if (bankData == null)
                    return false;
                else
                {
                    midiSynthesizer.LoadBank(new MyMemoryFile(bankData, filename));
                    midiSynthesizer.ResetSynthControls(); // Need to do this for bank to load properly, don't know why
                }
            }

            // Create sequencer
            if (midiSequencer == null)
                midiSequencer = new MidiFileSequencer(midiSynthesizer);

            // Check init
            if (midiSynthesizer == null || midiSequencer == null)
            {
                DaggerfallUnity.LogMessage("DynamicSongPlayer: Failed to init synth.");
                return false;
            }

            return true;
        }

        private string EnumToFilename(SongFiles song)
        {
            string enumName = song.ToString();
            return enumName.Remove(0, "song_".Length) + ".mid";
        }

        private byte[] LoadBank(string filename)
        {
            // Do nothing if no filename set
            if (string.IsNullOrEmpty(filename))
                return null;

            // Check file exists
            string path = Path.Combine(Application.streamingAssetsPath, sourceFolderName);
            string filePath = Path.Combine(path, filename);
            if (!File.Exists(filePath))
            {
                // Fallback to default sound font
                Debug.LogFormat("Could not find file '{0}', falling back to default soundfont {1}.", filePath, defaultSoundFontFilename);
                return null;
            }

            // Load data
            return File.ReadAllBytes(filePath);
        }

        private byte[] LoadDefaultSoundFont()
        {
            TextAsset asset = Resources.Load<TextAsset>(defaultSoundFontFilename);
            if (asset != null)
            {
                return asset.bytes;
            }

            DaggerfallUnity.LogMessage(string.Format("DynamicSongPlayer: Bank file '{0}' not found.", defaultSoundFontFilename));

            return null;
        }

        private void PlaySequencer(SongFiles song)
        {
            // Load song data
            string filename = EnumToFilename(song);
            byte[] songData = LoadSong(filename);
            if (songData == null)
                return;

            // Create song
            MidiFile midiFile = new MidiFile(new MyMemoryFile(songData, filename));
            if (midiSequencer.LoadMidi(midiFile))
            {
                midiSequencer.Play();
                Song = song;
                playEnabled = true;
                IsSequencerPlaying = true;
            }
        }

        private byte[] LoadSong(string filename)
        {
            // Get custom midi song
            if (SoundReplacement.TryImportMidiSong(filename, out byte[] songBytes))
                return songBytes;

            // Get Daggerfal song
            TextAsset asset = Resources.Load<TextAsset>(Path.Combine(SongFolder, filename));
            if (asset != null)
            {
                return asset.bytes;
            }

            DaggerfallUnity.LogMessage(string.Format("DynamicSongPlayer: Song file '{0}' not found.", filename));

            return null;
        }

        private void DaggerfallVidPlayerWindow_OnVideoStart()
        {
            // Mute music while video is playing
            oldGain = Gain;
            Gain = 0;
        }

        private void DaggerfallVidPlayerWindow_OnVideoEnd()
        {
            // Restore music to previous level
            Gain = oldGain;
        }

        private bool TryLoadSong(string path, out AudioClip audioClip)
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

        // Called when audio filter needs more sound data
        void OnAudioFilterRead(float[] data, int channels)
        {
            // Do nothing if play not enabled
            // This flag is raised/lowered when user starts/stops play
            // Helps avoids thread finding synth in state of shutting down
            if (!playEnabled)
                return;

            // Must have synth and seq
            if (midiSynthesizer == null || midiSequencer == null)
                return;

            // Sample buffer size must match working buffer size
            if (sampleBuffer.Length != midiSynthesizer.WorkingBufferSize)
                sampleBuffer = new float[midiSynthesizer.WorkingBufferSize];

            try
            {
                // Update sequencing - must be playing a song
                if (midiSequencer.IsMidiLoaded && midiSequencer.IsPlaying)
                {
                    midiSequencer.FillMidiEventQueue();
                    midiSynthesizer.GetNext(sampleBuffer);
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = sampleBuffer[i] * Gain;
                    }
                }
            }
            catch (Exception)
            {
                // Will sometimes drop here if Unity tries to feed audio filter
                // from another thread while synth is starting up or shutting down
                // Just nom the exception
            }
        }

        public class MyMemoryFile : DaggerfallWorkshop.AudioSynthesis.IResource
        {
            private byte[] file;
            private string fileName;
            public MyMemoryFile(byte[] file, string fileName)
            {
                this.file = file;
                this.fileName = fileName;
            }
            public string GetName() { return fileName; }
            public bool DeleteAllowed() { return false; }
            public bool ReadAllowed() { return true; }
            public bool WriteAllowed() { return false; }
            public void DeleteResource() { return; }
            public Stream OpenResourceForRead() { return new MemoryStream(file); }
            public Stream OpenResourceForWrite() { return null; }
        }
    }
}