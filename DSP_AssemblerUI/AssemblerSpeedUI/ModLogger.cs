using System.Diagnostics;
using BepInEx.Logging;

namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    public class ModLogger
    {
        private readonly ManualLogSource _logSource;

        public ModLogger(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        [Conditional("DEBUG")]
        public void DebugLog(string logMessage)
        {
            _logSource.LogDebug(logMessage);
        }

        public void ErrorLog(string logMessage)
        {
            _logSource.LogError(logMessage);
        }
    }
}