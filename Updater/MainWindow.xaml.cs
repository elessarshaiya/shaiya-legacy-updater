using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Shaiya_Invasion_Updater
{
    public partial class MainWindow : Window
    {
        public static MainWindow MW;
        private iMenuItem LinkAnotherAccount;
        public static List<string> Accounts = new List<string>();
        public string UserID;
        public string TempPw;
        public byte? LogInType;
        private bool _launchingGame;

        public MainWindow()
        {
            InitializeComponent();
            AppConfig.Load();
            MW = this;
            LinkWebsite.Link = AppConfig.WebsiteUrl;
            LinkRegister.Link = AppConfig.RegisterUrl;
            Updater.Continue();

            LinkAnotherAccount = new iMenuItem
            {
                Text = "Log in using another account",
                Link = "Updater:LogInNewAccount",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 18.0
            };
            RefreshLogInView();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private ImageBrush Brush(string uri) { return new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Resources/" + uri))); }

        private void ButtonMinimize_MouseEnter(object sender, MouseEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize_hover.jpg"); }
        private void ButtonMinimize_MouseLeave(object sender, MouseEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize.png"); }
        private void ButtonMinimize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize_active.jpg"); }
        private void ButtonMinimize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize.png"); WindowState = WindowState.Minimized; }
        private void ButtonClose_MouseEnter(object sender, MouseEventArgs e) { ButtonClose.Fill = Brush("button_close_hover.jpg"); }
        private void ButtonClose_MouseLeave(object sender, MouseEventArgs e) { ButtonClose.Fill = Brush("button_close.png"); }
        private void ButtonClose_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { ButtonClose.Fill = Brush("button_close_active.jpg"); }
        private void ButtonClose_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { ButtonClose.Fill = Brush("button_close.png"); Close(); }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_launchingGame) e.Cancel = true;
        }

        public void ShowLatestNews()
        {
            DoubleAnimation a = new DoubleAnimation { From = 0.0, To = 960.0, Duration = new Duration(TimeSpan.FromMilliseconds(400.0)) };
            Storyboard.SetTarget(a, GridSlide);
            Storyboard.SetTargetProperty(a, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            Storyboard sb = new Storyboard();
            sb.Children.Add(a);
            sb.Begin();
            LinkLogIn.IsSelected = false;
            LinkLatestNews.IsSelected = true;
        }

        public void ShowLogIn()
        {
            DoubleAnimation a = new DoubleAnimation { From = 960.0, To = 0.0, Duration = new Duration(TimeSpan.FromMilliseconds(400.0)) };
            Storyboard.SetTarget(a, GridSlide);
            Storyboard.SetTargetProperty(a, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            Storyboard sb = new Storyboard();
            sb.Children.Add(a);
            sb.Begin();
            LinkLogIn.IsSelected = true;
            LinkLatestNews.IsSelected = false;
        }

        public void RefreshExistingAccounts()
        {
            AccountRectangles.Children.Clear();
            if (Accounts.Count <= 0) return;
            foreach (string account in Accounts) AccountRectangles.Children.Add(new Account(account));
            AccountRectangles.Children.Add(LinkAnotherAccount);
            LogInButton.Margin = new Thickness(0.0, 210.0, 0.0, 0.0);
            LinkSavedAccounts.Visibility = Visibility.Visible;
            LinkAnotherAccount.Visibility = Accounts.Count >= 8 ? Visibility.Hidden : Visibility.Visible;
        }

        public void RefreshLogInView(bool savedAccounts = true)
        {
            if (savedAccounts && Accounts.Count > 0)
            {
                RefreshExistingAccounts();
                GridSavedAccounts.Visibility = Visibility.Visible;
                GridNewAccount.Visibility = Visibility.Hidden;
            }
            else
            {
                GridSavedAccounts.Visibility = Visibility.Hidden;
                GridNewAccount.Visibility = Visibility.Visible;
                TextBoxUserID.Focus();
            }
        }

        private void GridNewAccount_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) LogInIdPassword();
        }

        private void LogInButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { LogInIdPassword(); }

        private void LogInIdPassword()
        {
            string userId = TextBoxUserID.Text.Trim();
            string password = TextBoxPassword.Password;
            bool saveAccount = CheckBoxSaveAccount.IsChecked == true;
            if (userId.Length == 0 || password.Length == 0) return;
            UserID = userId;
            UpdaterManager.LogInIdPassword(userId, password, saveAccount);
        }

        public void LogInId(string userId)
        {
            UserID = userId;
            UpdaterManager.LogInId(userId);
        }

        public void LogOut()
        {
            GameStartButton.Visibility = Visibility.Hidden;
            LinkLogIn.Text = "Log In";
            LinkLogIn.Link = "Updater:LogIn";
            LinkLogIn.IsSelected = false;
            LinkLogIn.Cursor = Cursors.Hand;
            LinkRegister.Text = "Sign Up";
            LinkRegister.Link = AppConfig.RegisterUrl;
            ShowLogIn();
            if (LogInType.GetValueOrDefault() == 0 && LogInType.HasValue) GridNewAccount.Visibility = Visibility.Visible;
            else GridSavedAccounts.Visibility = Visibility.Visible;
            UserID = null;
            TempPw = null;
            RefreshLogInView();
        }

        public void StartRepair()
        {
            if (MessageDialog.Display("Repair", "This will verify client files and re-download broken/missing files. Continue?", "YesNo") != "Yes") return;
            Updater.Repair();
        }

        public void CanStartGame()
        {
            if (UserID == null || !Updater.FinishedUpdating) return;
            GameStartButton.Visibility = Visibility.Visible;
        }

        private void GameStartButton_Click(object sender, MouseButtonEventArgs e)
        {
            string gamePath = Path.Combine(AppPaths.BaseDirectory, AppConfig.GameExecutable);
            if (!File.Exists(gamePath))
            {
                MessageDialog.Display("Error", AppConfig.GameExecutable + " was not found.");
                return;
            }

            if (GameProcessGuard.IsGameRunning())
            {
                MessageDialog.Display("Error", AppConfig.GameExecutable + " is already running.");
                return;
            }

            _launchingGame = true;

            if (AppConfig.HideLoginScreens)
            {
                ShowGameLaunchPanel(AppConfig.LoginOverlayText, AppConfig.LoginOverlaySubText);
                AppLogger.Info("Launcher launch panel opened. Game window will stay hidden until character selection is ready.");
            }
            else
            {
                GameStartButton.Visibility = Visibility.Hidden;
                AppLogger.Info("Launcher launch panel disabled by launcher.ini.");
            }

            Thread launchThread = new Thread(delegate()
            {
                LaunchGameWorker(gamePath);
            });
            launchThread.Name = "GameLaunchFlow";
            launchThread.IsBackground = true;
            launchThread.Start();
        }

        private static void LaunchGameWorker(string gamePath)
        {
            int processId = 0;
            GameWindowController gameWindow = null;
            bool success = false;

            try
            {
                SetGameLaunchStatus("Starting game...", AppConfig.LoginOverlaySubText);

                ProcessStartInfo psi = new ProcessStartInfo(gamePath, AppConfig.GameArguments)
                {
                    WorkingDirectory = AppPaths.BaseDirectory
                };

                Process process = Process.Start(psi);
                if (process == null) throw new InvalidOperationException("Game process could not be started.");

                processId = process.Id;
                gameWindow = GameWindowController.HideUntilReady(processId);
                TryInjectCredentials(processId, out success);
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                if (processId != 0)
                {
                    try { Process.GetProcessById(processId).Kill(); } catch { }
                }
                ShowLaunchFailed("Launch failed", "Please check updater.log and try again.");
            }
            finally
            {
                if (success)
                {
                    try { if (gameWindow != null) gameWindow.Restore(); } catch { }
                    Environment.Exit(0);
                }
                else
                {
                    try { if (gameWindow != null) gameWindow.Restore(); } catch { }
                    ResetGameLaunchPanel();
                }
            }
        }

        private static void TryInjectCredentials(int processId, out bool success)
        {
            bool autoSelected = false;
            success = false;

            Thread.Sleep(2000);
            int pointer = 0;
            for (int i = 0; i < 150; i++)
            {
                pointer = BitConverter.ToInt32(MemoryManager.ReadMemory(processId, GameOffsets.LoginPointer, 4), 0);
                if (pointer != 0) break;
                Thread.Sleep(200);
            }
            if (pointer == 0) throw new InvalidOperationException("Game login buffer pointer was not initialized.");

            SetGameLaunchStatus("Logging in...", "Connecting with your account.");
            InjectCredentials(processId, pointer);

            try
            {
                SetGameLaunchStatus("Selecting server...", "Preparing character list.");
                autoSelected = GameServerSelectAutomation.AutoSelectFirstServer(processId);
            }
            catch (Exception autoEx)
            {
                // Auto server selection is optional. Never kill the game for this step;
                // the player can still select the server manually.
                AppLogger.Warn("Auto server select failed without terminating game: " + autoEx.Message);
            }

            if (autoSelected)
            {
                SetGameLaunchStatus("Loading characters...", "Almost ready.");
                Thread.Sleep(AppConfig.LoginOverlayRevealDelayMs);
            }
            else
            {
                SetGameLaunchStatus("Manual server selection required", "Please continue in the game window.");
                Thread.Sleep(AppConfig.LoginOverlayFallbackRevealDelayMs);
            }

            success = true;
        }

        private void ShowGameLaunchPanel(string title, string subtitle)
        {
            LinkWebsite.IsEnabled = false;
            LinkLatestNews.IsEnabled = false;
            LinkRepair.IsEnabled = false;
            LinkLogIn.IsEnabled = false;
            LinkRegister.IsEnabled = false;
            GameStartButton.Visibility = Visibility.Hidden;
            GameLaunchPanel.Visibility = Visibility.Visible;
            SetGameLaunchStatusOnUi(title, subtitle);
        }

        private static void SetGameLaunchStatus(string title, string subtitle)
        {
            try
            {
                if (MW == null) return;
                MW.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (MW != null) MW.SetGameLaunchStatusOnUi(title, subtitle);
                }));
            }
            catch { }
        }

        private void SetGameLaunchStatusOnUi(string title, string subtitle)
        {
            if (!string.IsNullOrWhiteSpace(title)) GameLaunchTitle.Text = title;
            if (subtitle != null) GameLaunchSubText.Text = subtitle;
        }

        private static void ShowLaunchFailed(string title, string subtitle)
        {
            try
            {
                if (MW == null) return;
                MW.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (MW == null) return;
                    MW.SetGameLaunchStatusOnUi(title, subtitle);
                    MessageDialog.Display("Error", subtitle);
                }));
            }
            catch { }
        }

        private static void ResetGameLaunchPanel()
        {
            try
            {
                if (MW == null) return;
                MW.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (MW == null) return;
                    MW._launchingGame = false;
                    MW.GameLaunchPanel.Visibility = Visibility.Collapsed;
                    MW.LinkWebsite.IsEnabled = true;
                    MW.LinkLatestNews.IsEnabled = true;
                    MW.LinkRepair.IsEnabled = true;
                    MW.LinkLogIn.IsEnabled = true;
                    MW.LinkRegister.IsEnabled = true;
                    MW.CanStartGame();
                }));
            }
            catch { }
        }

        private static void InjectCredentials(int processId, int pointer)
        {
            MemoryManager.WriteFixedAscii(processId, pointer + GameOffsets.UserIdOffsetFromPointer, MW.UserID, GameOffsets.UserIdBufferLength);
            MemoryManager.WriteFixedAscii(processId, pointer + GameOffsets.TempPasswordOffsetFromPointer, MW.TempPw, GameOffsets.TempPasswordBufferLength);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow1, GameOffsets.PatchLoginFlow1Bytes);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow2, GameOffsets.PatchLoginFlow2Bytes);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow3, GameOffsets.PatchLoginFlow3Bytes);
        }
    }
}
