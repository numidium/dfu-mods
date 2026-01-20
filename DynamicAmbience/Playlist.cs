/// <summary>
/// Holds a list of strings that represent music tracks. Advances track and auto shuffles.
/// </summary>
namespace DynamicAmbience {
    public sealed class Playlist
    {
        private readonly string[] tracks;
        private int index = 0;
        public int TrackCount => tracks.Length;
        public string CurrentTrack => tracks[index];
        public float MinDelay { get; private set; }
        public float MaxDelay { get; private set; }
        public bool IsPositional { get; private set; }

        public Playlist(string[] trackList, float minDelay, float maxDelay, bool isPositional = false)
        {
            tracks = trackList;
            MinDelay = minDelay;
            MaxDelay = maxDelay;
            IsPositional = isPositional;
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
}