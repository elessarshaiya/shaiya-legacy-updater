using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;

namespace Shaiya_Invasion_Updater
{
    public static class Updater
    {
        public static bool Ready;
        public static bool FinishedUpdating;
        public static bool DownloadingUpdater;
        public static int Local_ClientVersion;
        public static int Server_ClientVersion;
        public static int Server_UpdaterVersion;

        private static Thread _thread;
        private static LauncherManifest _manifest;

        public static void Start()
        {
            _thread = new Thread(StartUpdater) { IsBackground = true };
            _thread.Start();
        }

        private static void StartUpdater()
        {
            try
            {
                AppConfig.Load();
                Local_ClientVersion = VersionStore.ReadClientVersion();
                _manifest = JsonHttpClient.Get<LauncherManifest>(AppConfig.ManifestUrl);
                if (_manifest == null) _manifest = new LauncherManifest { clientVersion = Local_ClientVersion, launcherVersion = AppConfig.LocalLauncherVersion };
                Server_ClientVersion = _manifest.clientVersion;
                Server_UpdaterVersion = _manifest.launcherVersion;
                if (_manifest.maintenance)
                {
                    string msg = string.IsNullOrEmpty(_manifest.maintenanceMessage) ? AppConfig.MaintenanceMessage : _manifest.maintenanceMessage;
                    throw new InvalidOperationException(msg);
                }
                Ready = true;
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                Server_ClientVersion = Local_ClientVersion;
                Server_UpdaterVersion = AppConfig.LocalLauncherVersion;
                Ready = true;
            }
        }

        public static void Continue()
        {
            _thread = new Thread(ContinueUpdater) { IsBackground = true };
            _thread.Start();
        }

        private static void ContinueUpdater()
        {
            try
            {
                AppConfig.Load();
                if (_manifest == null)
                {
                    try { _manifest = JsonHttpClient.Get<LauncherManifest>(AppConfig.ManifestUrl); }
                    catch { _manifest = new LauncherManifest { clientVersion = Local_ClientVersion, launcherVersion = AppConfig.LocalLauncherVersion }; }
                }

                if (NeedsLauncherUpdate(_manifest))
                {
                    UpdateUpdater(_manifest.launcherFile);
                    return;
                }

                if (Local_ClientVersion < _manifest.clientVersion || AppConfig.ForceUpdate)
                    UpdateClient(_manifest);

                FinishedUpdating = true;
                Utilities.ExecuteOnMainThread(delegate()
                {
                    if (MainWindow.MW == null) return;
                    MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                    MainWindow.MW.TextBlockUpdaterStatus.Text = "";
                    MainWindow.MW.CanStartGame();
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                Utilities.ExecuteOnMainThread(delegate()
                {
                    if (MainWindow.MW != null)
                    {
                        MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                        MainWindow.MW.TextBlockUpdaterStatus.Text = "Update failed.";
                    }
                    MessageDialog.Display("Update Error", ex.Message);
                });
            }
        }

        public static void Repair()
        {
            Thread thread = new Thread(delegate()
            {
                try
                {
                    AppConfig.Load();
                    LauncherManifest manifest = _manifest ?? JsonHttpClient.Get<LauncherManifest>(AppConfig.ManifestUrl);
                    if (!AppConfig.RepairEnabled) throw new InvalidOperationException("Repair is disabled by server configuration.");

                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW == null) return;
                        MainWindow.MW.GameStartButton.Visibility = Visibility.Hidden;
                        MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Visible;
                        MainWindow.MW.UpdaterProgressBar.SetProgress(0);
                        MainWindow.MW.TextBlockUpdaterStatus.Text = "Starting repair";
                    });

                    RepairService.VerifyAndRepair(manifest, delegate(int percent, string message)
                    {
                        Utilities.ExecuteOnMainThread(delegate()
                        {
                            if (MainWindow.MW == null) return;
                            MainWindow.MW.UpdaterProgressBar.SetProgress((byte)Math.Max(0, Math.Min(100, percent)));
                            MainWindow.MW.TextBlockUpdaterStatus.Text = message;
                        });
                    });

                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW == null) return;
                        MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                        MainWindow.MW.TextBlockUpdaterStatus.Text = "Repair completed.";
                        MainWindow.MW.CanStartGame();
                        MessageDialog.Display("Repair", "Repair completed.");
                    });
                }
                catch (Exception ex)
                {
                    ExceptionManager.Submit(ex);
                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW != null)
                        {
                            MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                            MainWindow.MW.TextBlockUpdaterStatus.Text = "Repair failed.";
                        }
                        MessageDialog.Display("Repair Error", ex.Message);
                    });
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private static bool NeedsLauncherUpdate(LauncherManifest manifest)
        {
            if (!AppConfig.SelfUpdateEnabled) return false;
            if (manifest == null || string.IsNullOrEmpty(manifest.launcherFile)) return false;
            int min = Math.Max(AppConfig.MinLauncherVersion, manifest.minLauncherVersion);
            return manifest.launcherVersion > AppConfig.LocalLauncherVersion || min > AppConfig.LocalLauncherVersion;
        }

        private static void UpdateUpdater(string launcherFile)
        {
            try
            {
                DownloadingUpdater = true;
                Utilities.ExecuteOnMainThread(delegate()
                {
                    MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Visible;
                    MainWindow.MW.TextBlockUpdaterStatus.Text = "Downloading launcher update";
                });

                string url = AppConfig.CombineUrl(AppConfig.BaseUrl, launcherFile);
                string newExe = AppPaths.InBase("Updater.exe.new");
                DownloadFileWithRetry(url, newExe, null);

                string bat = AppPaths.InBase("apply_launcher_update.bat");
                File.WriteAllText(bat,
                    "@echo off\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +
                    "del /f /q \"Updater.exe.bak\" >nul 2>nul\r\n" +
                    "ren \"Updater.exe\" \"Updater.exe.bak\"\r\n" +
                    "ren \"Updater.exe.new\" \"Updater.exe\"\r\n" +
                    "start \"\" \"Updater.exe\"\r\n" +
                    "del /f /q \"%~f0\" >nul 2>nul\r\n");
                Process.Start(new ProcessStartInfo(bat) { WorkingDirectory = AppPaths.BaseDirectory, WindowStyle = ProcessWindowStyle.Hidden });
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DownloadingUpdater = false;
                throw new InvalidOperationException("Launcher update failed: " + ex.Message, ex);
            }
        }

        private static void UpdateClient(LauncherManifest manifest)
        {
            GameProcessGuard.ThrowIfGameRunningForPatch();

            Utilities.ExecuteOnMainThread(delegate()
            {
                MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Visible;
                MainWindow.MW.TextBlockUpdaterStatus.Text = "Preparing patches";
            });

            PatchManifestItem[] patches = manifest.patches ?? new PatchManifestItem[0];
            for (int version = Local_ClientVersion + 1; version <= manifest.clientVersion; version++)
            {
                PatchManifestItem item = patches.FirstOrDefault(p => p.version == version);
                if (item == null) throw new FileNotFoundException("Patch manifest entry missing for version " + version + ".");

                VersionStore.StartPatch(Local_ClientVersion, item);
                try
                {
                    ApplyPatch(item);
                    Local_ClientVersion = version;
                    VersionStore.CommitPatch(Local_ClientVersion, item);
                }
                catch (Exception ex)
                {
                    VersionStore.FailPatch(Local_ClientVersion, item, ex.Message);
                    throw;
                }
            }
        }

        private static void ApplyPatch(PatchManifestItem item)
        {
            string tempRoot = AppPaths.InBase(Path.Combine("patch", "tmp_" + item.version.ToString("D4")));
            string downloadDir = AppPaths.InBase("patch");
            Directory.CreateDirectory(downloadDir);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            Directory.CreateDirectory(tempRoot);

            string patchFile = PatchPathUtil.GetPatchLocalPath(downloadDir, item);
            string tmpDownload = patchFile + ".download";
            string extractDir = Path.Combine(tempRoot, "extract");

            try
            {
                AppLogger.Patch("Patch apply started. Version=" + item.version + " File=" + item.file);
                Utilities.ExecuteOnMainThread(delegate()
                {
                    MainWindow.MW.TextBlockUpdaterStatus.Text = "Downloading " + Path.GetFileName(item.file);
                    MainWindow.MW.UpdaterProgressBar.SetProgress(0);
                });

                string url = PatchPathUtil.GetPatchUrl(item);
                DownloadFileWithRetry(url, tmpDownload, wc_DownloadProgressChanged);
                if (File.Exists(patchFile)) File.Delete(patchFile);
                File.Move(tmpDownload, patchFile);

                ValidatePatchFile(item, patchFile);

                Utilities.ExecuteOnMainThread(delegate() { MainWindow.MW.TextBlockUpdaterStatus.Text = "Extracting patch"; });
                PatchExtractor.ExtractZipSafe(patchFile, extractDir);

                Utilities.ExecuteOnMainThread(delegate() { MainWindow.MW.TextBlockUpdaterStatus.Text = "Installing patch"; });
                PatchApplier.ApplyExtractedPatch(extractDir, item);
                AppLogger.Patch("Patch apply completed. Version=" + item.version);
            }
            finally
            {
                try { if (File.Exists(tmpDownload)) File.Delete(tmpDownload); } catch { }
                try { if (File.Exists(patchFile)) File.Delete(patchFile); } catch { }
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static void ValidatePatchFile(PatchManifestItem item, string patchFile)
        {
            if (item.size > 0)
            {
                long actualSize = new FileInfo(patchFile).Length;
                if (actualSize != item.size)
                    throw new InvalidDataException("Patch size mismatch for " + item.file + ". Expected=" + item.size + " Actual=" + actualSize);
            }

            if (!string.IsNullOrWhiteSpace(item.sha256))
            {
                string actual = HashUtil.Sha256File(patchFile);
                if (!string.Equals(actual, item.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Patch SHA256 mismatch for " + item.file + ". Expected=" + item.sha256 + " Actual=" + actual);
            }

            AppLogger.Patch("Patch validation OK. File=" + item.file + " Size=" + new FileInfo(patchFile).Length);
        }

        private static void DownloadFileWithRetry(string url, string target, DownloadProgressChangedEventHandler progress)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= AppConfig.DownloadRetryCount; attempt++)
            {
                try
                {
                    if (File.Exists(target)) File.Delete(target);
                    using (TimeoutWebClient wc = CreateWebClient())
                    {
                        if (progress != null) wc.DownloadProgressChanged += progress;
                        wc.DownloadFile(new Uri(url), target);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppLogger.PatchWarn("Download attempt " + attempt + " failed: " + url + " - " + ex.Message);
                    try { if (File.Exists(target)) File.Delete(target); } catch { }
                    Thread.Sleep(500);
                }
            }
            throw new InvalidOperationException("Download failed: " + url + " - " + (last == null ? "unknown error" : last.Message), last);
        }

        private static TimeoutWebClient CreateWebClient()
        {
            TimeoutWebClient wc = new TimeoutWebClient(AppConfig.DownloadTimeoutSeconds * 1000);
            wc.Headers[HttpRequestHeader.UserAgent] = "LegacyShaiyaUpdater/1.0";
            return wc;
        }

        private static void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Utilities.ExecuteOnMainThread(delegate()
            {
                if (MainWindow.MW == null) return;
                MainWindow.MW.UpdaterProgressBar.SetProgress((byte)Math.Max(0, Math.Min(100, e.ProgressPercentage)));
                string speedInfo = e.TotalBytesToReceive > 0 ? " " + FormatBytes(e.BytesReceived) + "/" + FormatBytes(e.TotalBytesToReceive) : string.Empty;
                MainWindow.MW.TextBlockUpdaterStatus.Text = "Downloading patch" + speedInfo;
            });
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.0") + " KB";
            double mb = kb / 1024.0;
            return mb.ToString("0.0") + " MB";
        }
    }
}
