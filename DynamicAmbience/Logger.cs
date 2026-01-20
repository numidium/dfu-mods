using UnityEngine;

namespace DynamicAmbience
{
    public sealed class Logger
    {
        public const string ModSignature = "Dynamic Ambience";
        static public void PrintInitMessage()
        {
            Debug.Log($"{ModSignature} initialized.");
        }

        static public void PrintLog(string text)
        {
            Debug.Log($"{ModSignature}: {text}");
        }
    }
}