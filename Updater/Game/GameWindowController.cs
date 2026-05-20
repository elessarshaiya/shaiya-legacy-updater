using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Shaiya_Invasion_Updater
{
    public sealed class GameWindowController : IDisposable
    {
        private readonly IntPtr _hwnd;
        private readonly RECT _originalRect;
        private readonly bool _hadOriginalRect;
        private bool _restored;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        private GameWindowController(IntPtr hwnd, RECT originalRect, bool hadOriginalRect)
        {
            _hwnd = hwnd;
            _originalRect = originalRect;
            _hadOriginalRect = hadOriginalRect;
        }

        public static GameWindowController HideUntilReady(int processId)
        {
            if (!AppConfig.HideGameWindowUntilReady) return null;

            try
            {
                IntPtr hwnd = WaitForMainWindow(processId, AppConfig.GameWindowHideWaitMs);
                if (hwnd == IntPtr.Zero)
                {
                    AppLogger.Warn("Game window hide skipped: main window handle was not found.");
                    return null;
                }

                RECT rect;
                bool hasRect = GetWindowRect(hwnd, out rect);
                GameWindowController controller = new GameWindowController(hwnd, rect, hasRect);

                string mode = (AppConfig.GameWindowHideMode ?? "Offscreen").Trim();
                if (mode.Equals("Hide", StringComparison.OrdinalIgnoreCase))
                {
                    ShowWindow(hwnd, SW_HIDE);
                    AppLogger.Info("Game window hidden until character screen is ready.");
                }
                else
                {
                    int width = hasRect && rect.Width > 0 ? rect.Width : 1024;
                    int height = hasRect && rect.Height > 0 ? rect.Height : 768;
                    SetWindowPos(hwnd, IntPtr.Zero, AppConfig.GameWindowOffscreenX, AppConfig.GameWindowOffscreenY, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
                    AppLogger.Info("Game window moved offscreen until character screen is ready. hwnd=0x" + hwnd.ToInt32().ToString("X8"));
                }

                return controller;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Game window hide failed: " + ex.Message);
                return null;
            }
        }

        private static IntPtr WaitForMainWindow(int processId, int waitMs)
        {
            int waited = 0;
            int step = 100;
            while (waited <= waitMs)
            {
                try
                {
                    Process process = Process.GetProcessById(processId);
                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
                }
                catch
                {
                    return IntPtr.Zero;
                }

                Thread.Sleep(step);
                waited += step;
            }

            return IntPtr.Zero;
        }

        public void Restore()
        {
            if (_restored) return;
            _restored = true;

            try
            {
                if (_hwnd == IntPtr.Zero || !IsWindow(_hwnd)) return;

                ShowWindow(_hwnd, SW_SHOW);
                ShowWindow(_hwnd, SW_RESTORE);

                if (_hadOriginalRect && _originalRect.Width > 0 && _originalRect.Height > 0)
                {
                    SetWindowPos(_hwnd, IntPtr.Zero, _originalRect.Left, _originalRect.Top, _originalRect.Width, _originalRect.Height, SWP_NOZORDER);
                }

                SetForegroundWindow(_hwnd);
                AppLogger.Info("Game window restored and focused.");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Game window restore failed: " + ex.Message);
            }
        }

        public void Dispose()
        {
            Restore();
        }
    }
}
