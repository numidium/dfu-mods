namespace DynamicAmbience
{
    public sealed class DynamicAmbienceSettings
    {   
        public enum LoggingLevels
        {
            Minimal,
            Verbose
        }

        private DynamicAmbienceSettings() { }
        private static DynamicAmbienceSettings instance;
        public static DynamicAmbienceSettings Instance
        {
            get
            {
                if (instance == null)
                    instance = new DynamicAmbienceSettings();
                return instance;
            }
        }

        public float VolumeLevel { get; set; }
        public LoggingLevels LoggingLevel { get; set; }
    }
}