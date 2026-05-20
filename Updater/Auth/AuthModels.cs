namespace Shaiya_Invasion_Updater
{
    public class LoginResponse
    {
        public bool ok { get; set; }
        public string message { get; set; }
        public string userId { get; set; }
        public int userUid { get; set; }
        public string passwordForGame { get; set; }
    }

    public class SavedAccountsResponse
    {
        public bool ok { get; set; }
        public string message { get; set; }
        public string[] accounts { get; set; }
    }

    public class GenericApiResponse
    {
        public bool ok { get; set; }
        public string message { get; set; }
    }
}
