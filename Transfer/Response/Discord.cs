namespace csharp_api.Transfer.Response.Discord
{
    public class DiscordToken
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
        public string token_type { get; set; }
        public long expires_in { get; set; }
    }

    public class DiscordUser
    {
        public string id { get; set; }
    }
}