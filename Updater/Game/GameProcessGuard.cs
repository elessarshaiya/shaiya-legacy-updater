using System;
using System.Diagnostics;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public static class GameProcessGuard
    {
        public static bool IsGameRunning()
        {
            try
            {
                string exe = string.IsNullOrWhiteSpace(AppConfig.GameExecutable) ? "game.exe" : AppConfig.GameExecutable;
                string name = Path.GetFileNameWithoutExtension(exe);
                if (string.IsNullOrWhiteSpace(name)) name = "game";
                return Process.GetProcessesByName(name).Length > 0;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Game process check failed: " + ex.Message);
                return false;
            }
        }

        public static void ThrowIfGameRunningForPatch()
        {
            if (AppConfig.BlockPatchWhenGameRunning && IsGameRunning())
                throw new InvalidOperationException(AppConfig.GameExecutable + " is running. Close the game before patch/repair.");
        }
    }
}
