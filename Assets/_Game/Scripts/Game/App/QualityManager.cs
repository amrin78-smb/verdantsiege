using UnityEngine;

namespace VerdantSiege.App
{
    /// <summary>
    /// Low / Medium / High profiles (configured by the editor ProjectConfigurator). Automatic mode picks a tier
    /// from device capabilities; Low targets a stable 30 FPS, Medium/High 60 FPS.
    /// </summary>
    public static class QualityManager
    {
        public static readonly string[] Names = { "Low", "Medium", "High" };
        public static int CurrentLevel { get; private set; } = 1;
        public static string Reason { get; private set; } = "";

        public static int DetectTier(out string reason)
        {
            string gpu = SystemInfo.graphicsDeviceName ?? "";
            int ram = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            bool is32 = System.IntPtr.Size == 4;
            if (Application.isEditor || Application.platform == RuntimePlatform.LinuxPlayer ||
                Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
            { reason = "desktop"; return 2; }
            if (is32 || gpu.Contains("PowerVR") || gpu.Contains("Mali-4") || gpu.Contains("Mali-T") || gpu.Contains("Adreno (TM) 3") ||
                ram < 3000 || cores <= 4)
            { reason = $"low-end ({gpu}, {ram} MB, {cores} cores, {(is32 ? "32" : "64")}-bit)"; return 0; }
            if (ram >= 7000 && (gpu.Contains("Adreno (TM) 7") || gpu.Contains("Adreno (TM) 8") || gpu.Contains("Immortalis") ||
                                gpu.Contains("Xclipse") || gpu.Contains("Mali-G7") || gpu.Contains("Mali-G6")))
            { reason = $"high-end ({gpu}, {ram} MB)"; return 2; }
            reason = $"mid ({gpu}, {ram} MB)";
            return 1;
        }

        /// <param name="setting">-1 automatic, else explicit level</param>
        public static void Apply(int setting)
        {
            int detected = DetectTier(out string reason);
            int level = setting >= 0 && setting <= 2 ? setting : detected;
            if (setting >= 0) reason = "player choice";
            int count = QualitySettings.count;
            if (count > 0) QualitySettings.SetQualityLevel(Mathf.Clamp(level, 0, count - 1), true);
            Application.targetFrameRate = level == 0 ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            CurrentLevel = level;
            Reason = reason;
            Debug.Log($"[VS Quality] {Names[level]} ({reason}); GPU={SystemInfo.graphicsDeviceName}; " +
                      $"API={SystemInfo.graphicsDeviceType}; RAM={SystemInfo.systemMemorySize}MB; " +
                      $"ABI={(System.IntPtr.Size == 4 ? "32-bit" : "64-bit")}; target={Application.targetFrameRate}fps");
        }
    }
}
