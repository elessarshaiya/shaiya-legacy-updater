using System;

namespace Shaiya_Invasion_Updater
{
    public static class ExceptionManager
    {
        public static void Submit(Exception ex)
        {
            AppLogger.Error("Unhandled updater exception", ex);
        }
    }
}
