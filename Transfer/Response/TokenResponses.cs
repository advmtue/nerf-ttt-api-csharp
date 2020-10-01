using System.Text.Json.Serialization;

namespace csharp_api.Transfer.Response.Token
{
    public abstract class Default
    {
        [JsonPropertyName("token")]
        public string token { get; set; }

        [JsonPropertyName("token_type")]
        public string token_type { get; set; }
    }

    public class RegistrationTokenResponse : Default
    {
        public RegistrationTokenResponse(string token)
        {
            this.token = token;
            this.token_type = "registration";
        }
    }

    public class RefreshTokenResponse : Default
    {
        public RefreshTokenResponse(string token)
        {
            this.token = token;
            this.token_type = "refresh";
        }
    }

    public class AccessTokenResponse : Default
    {
        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }

        public AccessTokenResponse(string token)
        {
            this.token = token;
            this.token_type = "access";
        }

        public AccessTokenResponse() { }
    }
}