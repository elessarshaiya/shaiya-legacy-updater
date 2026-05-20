using System;
using System.Collections.Generic;

namespace Shaiya_Invasion_Updater
{
    public static class AuthClient
    {
        public static LoginResponse Login(string userId, string password, bool saveAccount)
        {
            string url = AppConfig.CombineUrl(AppConfig.BaseUrl, "api/auth/login.php");
            return JsonHttpClient.Post<LoginResponse>(url, new
            {
                userId = userId,
                password = password,
                saveAccount = saveAccount,
                deviceId = Utilities.Hardware.HWID
            });
        }

        public static LoginResponse LoginSaved(string userId)
        {
            string url = AppConfig.CombineUrl(AppConfig.BaseUrl, "api/auth/login-saved.php");
            return JsonHttpClient.Post<LoginResponse>(url, new
            {
                userId = userId,
                deviceId = Utilities.Hardware.HWID
            });
        }

        public static List<string> ReadSavedAccounts()
        {
            List<string> local = LocalAccountStore.Load();
            try
            {
                string url = AppConfig.CombineUrl(AppConfig.BaseUrl, "api/auth/saved-accounts.php");
                SavedAccountsResponse response = JsonHttpClient.Post<SavedAccountsResponse>(url, new
                {
                    deviceId = Utilities.Hardware.HWID
                });
                if (response != null && response.ok && response.accounts != null)
                {
                    foreach (string acc in response.accounts)
                    {
                        if (!local.Exists(x => string.Equals(x, acc, StringComparison.OrdinalIgnoreCase))) local.Add(acc);
                    }
                    LocalAccountStore.Save(local);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Server saved-account list unavailable, using local list. " + ex.Message);
            }
            return local;
        }

        public static void RemoveSavedAccount(string userId)
        {
            LocalAccountStore.Remove(userId);
            try
            {
                string url = AppConfig.CombineUrl(AppConfig.BaseUrl, "api/auth/remove-saved-account.php");
                JsonHttpClient.Post<GenericApiResponse>(url, new
                {
                    userId = userId,
                    deviceId = Utilities.Hardware.HWID
                });
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Server remove-saved-account failed. " + ex.Message);
            }
        }
    }
}
