using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Shaiya_Invasion_Updater
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            new Thread(InitializeConnections) { IsBackground = true }.Start();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void InitializeConnections()
        {
            try
            {
                AppConfig.Load();
                RemoveOldUpdater();
                VerifyProcessesAndGameDirectory();
                Utilities.Hardware.ReadInfo();
                Updater.Start();
                UpdaterManager.ReadSavedAccounts();

                for (int i = 0; i < 300; i++)
                {
                    if (UpdaterManager.Ready && Updater.Ready)
                    {
                        if (UpdaterManager.ReturnValue != 1)
                        {
                            SplashError("Error", "Failed to connect to the login server. Check C:\\inetpub\\wwwroot\\Updater\\launcher.ini and api config.php.");
                            return;
                        }
                        SplashSuccess();
                        return;
                    }
                    Thread.Sleep(100);
                }
                SplashError("Error", "Failed to connect to the server. Please try again later.");
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                SplashError("Error", ex.Message);
            }
        }

        private void RemoveOldUpdater()
        {
            string currentDirectory = AppPaths.BaseDirectory;
            string old = Path.Combine(currentDirectory, "OldUpdater.exe");
            if (!File.Exists(old)) return;
            foreach (Process process in Process.GetProcessesByName("Updater").Where(p => p.Id != Process.GetCurrentProcess().Id))
            {
                try
                {
                    if (Path.GetDirectoryName(process.MainModule.FileName) == currentDirectory) process.Kill();
                }
                catch { }
            }
            Thread.Sleep(500);
            try { File.Delete(old); } catch { }
        }

        private void VerifyProcessesAndGameDirectory()
        {
            Process current = Process.GetCurrentProcess();
            string updaterFile = current.MainModule.FileName;
            string directory = Path.GetDirectoryName(updaterFile);
            string gameFile = Path.Combine(directory, AppConfig.GameExecutable);

            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(updaterFile)).Any(p => p.Id != current.Id && SafeFile(p) == updaterFile))
                throw new InvalidOperationException("Running multiple clients at the same time is not allowed.");

            if (File.Exists(gameFile) && Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppConfig.GameExecutable)).Any(p => SafeFile(p) == gameFile))
                throw new InvalidOperationException("Running multiple clients at the same time is not allowed.");

            if (!AppConfig.RequireOriginalClientFiles) return;
            string[] required = { AppConfig.GameExecutable, "Updater.exe", "ijl15.dll", "Ionic.Zip.dll", "CONFIG.INI", "data.saf", "data.sah", "notice.txt", "tip.txt" };
            string[] existing = Directory.GetFiles(directory).Select(Path.GetFileName).ToArray();
            string[] missing = required.Where(r => !existing.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (missing.Length > 0) throw new InvalidOperationException("Your client is missing the following files:\r\n" + string.Join(", ", missing));
        }

        private static string SafeFile(Process p)
        {
            try { return p.MainModule.FileName; } catch { return string.Empty; }
        }

        private void SplashSuccess()
        {
            Utilities.ExecuteOnMainThread(delegate()
            {
                Hide();
                new MainWindow().Show();
                Close();
            });
        }

        private void SplashError(string title, string text)
        {
            Utilities.ExecuteOnMainThread(delegate()
            {
                MessageDialog.Display(title, text);
                Environment.Exit(0);
            });
        }
    }
}
