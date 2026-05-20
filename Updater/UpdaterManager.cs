using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Shaiya_Invasion_Updater
{
    public static class UpdaterManager
    {
        public static bool Ready;
        public static short ReturnValue;

        public static void ReadSavedAccounts()
        {
            Ready = false;
            ReturnValue = 0;
            new Thread(delegate()
            {
                try
                {
                    AppConfig.Load();
                    List<string> accounts = AuthClient.ReadSavedAccounts();
                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        MainWindow.Accounts.Clear();
                        foreach (string account in accounts)
                        {
                            if (!MainWindow.Accounts.Contains(account)) MainWindow.Accounts.Add(account);
                        }
                    });
                    ReturnValue = 1;
                }
                catch (Exception ex)
                {
                    ExceptionManager.Submit(ex);
                    ReturnValue = -1;
                }
                finally
                {
                    Ready = true;
                }
            }) { IsBackground = true }.Start();
        }

        public static void LogInIdPassword(string userId, string password, bool saveAccount)
        {
            BeginLoginUi(0);
            new Thread(delegate()
            {
                try
                {
                    LoginResponse response = AuthClient.Login(userId, password, saveAccount);
                    ReturnLogIn(response, saveAccount);
                }
                catch (Exception ex)
                {
                    ExceptionManager.Submit(ex);
                    ReturnLogIn(new LoginResponse { ok = false, message = "Login server is unavailable." }, saveAccount);
                }
            }) { IsBackground = true }.Start();
        }

        public static void LogInId(string userId)
        {
            BeginLoginUi(1);
            new Thread(delegate()
            {
                try
                {
                    LoginResponse response = AuthClient.LoginSaved(userId);
                    ReturnLogIn(response, false);
                }
                catch (Exception ex)
                {
                    ExceptionManager.Submit(ex);
                    ReturnLogIn(new LoginResponse { ok = false, message = "Saved-account login failed. Please enter your password again." }, false);
                }
            }) { IsBackground = true }.Start();
        }

        public static void RemoveSavedAccount(string userId)
        {
            AuthClient.RemoveSavedAccount(userId);
        }

        private static void BeginLoginUi(byte loginType)
        {
            Utilities.ExecuteOnMainThread(delegate()
            {
                if (MainWindow.MW == null) return;
                MainWindow.MW.LogInType = loginType;
                MainWindow.MW.GridNewAccount.Visibility = Visibility.Hidden;
                MainWindow.MW.GridSavedAccounts.Visibility = Visibility.Hidden;
                MainWindow.MW.LoadAnimation.Visibility = Visibility.Visible;
            });
        }

        private static void ReturnLogIn(LoginResponse response, bool saveAccount)
        {
            Utilities.ExecuteOnMainThread(delegate()
            {
                try
                {
                    if (MainWindow.MW == null) return;
                    if (response != null && response.ok && !string.IsNullOrEmpty(response.passwordForGame))
                    {
                        MainWindow.MW.TempPw = response.passwordForGame;
                        MainWindow.MW.LoadAnimation.Visibility = Visibility.Hidden;

                        if (saveAccount && !string.IsNullOrEmpty(MainWindow.MW.UserID))
                        {
                            LocalAccountStore.Add(MainWindow.MW.UserID);
                            if (!MainWindow.Accounts.Contains(MainWindow.MW.UserID)) MainWindow.Accounts.Add(MainWindow.MW.UserID);
                        }

                        MainWindow.MW.ShowLatestNews();
                        MainWindow.MW.LinkLogIn.Text = MainWindow.MW.UserID;
                        MainWindow.MW.LinkLogIn.Link = "";
                        MainWindow.MW.LinkLogIn.IsSelected = true;
                        MainWindow.MW.LinkLogIn.Cursor = Cursors.Arrow;
                        MainWindow.MW.LinkRegister.Text = "Log Out";
                        MainWindow.MW.LinkRegister.Link = "Updater:LogOut";
                        MainWindow.MW.CanStartGame();
                        MainWindow.MW.TextBoxUserID.Clear();
                        MainWindow.MW.TextBoxPassword.Clear();
                    }
                    else
                    {
                        string message = response != null && response.ok && string.IsNullOrEmpty(response.passwordForGame) ? "Login server did not return the game password." : (response == null || string.IsNullOrEmpty(response.message) ? "Login failed." : response.message);
                        MessageDialog.Display("Error", message);
                        MainWindow.MW.LoadAnimation.Visibility = Visibility.Collapsed;
                        if (MainWindow.MW.LogInType.GetValueOrDefault() == 0 && MainWindow.MW.LogInType.HasValue)
                        {
                            MainWindow.MW.GridNewAccount.Visibility = Visibility.Visible;
                            MainWindow.MW.TextBoxPassword.Focus();
                        }
                        else
                        {
                            MainWindow.MW.GridSavedAccounts.Visibility = Visibility.Visible;
                        }
                        MainWindow.MW.UserID = null;
                        MainWindow.MW.TempPw = null;
                        MainWindow.MW.LogInType = null;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.Submit(ex);
                }
            });
        }
    }
}
