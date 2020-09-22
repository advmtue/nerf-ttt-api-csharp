using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

using csharp_api.Database;
using csharp_api.Model.User;
using csharp_api.Model.User.Discord;
using csharp_api.Transfer.Response.Discord;

namespace csharp_api.Services.Discord
{
    public class DiscordAuthenticator : IAuthenticationService<Profile>
    {
        private readonly string _client_id;
        private readonly string _client_secret;
        private readonly HttpClient client = new HttpClient();
        private readonly string _discordTokenURI;
        private readonly string _discordUserURI;
        private readonly IDatabase _database;
        private readonly IConfiguration _config;
        private readonly string _redirect_uri;

        public DiscordAuthenticator(IDatabase database, IConfiguration config)
        {
            _database = database;
            _config = config.GetSection("DiscordSecrets");

            // Read client_id and secret from configuration
            this._client_id = _config["client_id"]; 
            this._client_secret = _config["client_secret"];
            this._redirect_uri = _config["redirect_uri"];

            this._discordTokenURI = "https://discord.com/api/oauth2/token";
            this._discordUserURI = "https://discord.com/api/users/@me";
        }

        public async Task<Profile> Authenticate(string code)
        {
            DiscordUser discordUser;
            try
            {
                // Make a token request
                DiscordToken discordToken = await _TokenRequest(code);
                // Make a user request
                discordUser = await _UserRequest(discordToken);
            }
            catch (Exception)
            {
                throw new AuthProviderErrorException();
            }

            // Lookup or create user by id
            Profile profile;
            try
            {
                // Attempt to pull profile
                profile = await _database.GetUserByDiscord(discordUser);
            }
            catch (UserNotFoundException)
            {
                // User doesn't exist, create it
                profile = await _database.CreateUserByDiscord(discordUser);
            }
            catch (Exception)
            {
                throw new UserLookupErrorException();
            }

            // Return the user
            return profile;
        }

        private async Task<DiscordToken> _TokenRequest(string code)
        {
            // Request body collection
            Dictionary<string, string> requestBody = new Dictionary<string, string>();
            requestBody.Add("client_id", this._client_id);
            requestBody.Add("client_secret", this._client_secret);
            requestBody.Add("grant_type", "authorization_code");
            requestBody.Add("code", code);
            requestBody.Add("redirect_uri", _redirect_uri);
            requestBody.Add("scope", "identify");

            // Encode the body content
            var bodyContent = new FormUrlEncodedContent(requestBody);

            // Build the request
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, this._discordTokenURI)
            {
                Content = bodyContent
            };

            // Perform the request
            var response = await this.client.SendAsync(tokenRequest);

            // Throw an error if the request failed for any reason
            response.EnsureSuccessStatusCode();

            // Deserialize request
            return JsonSerializer.Deserialize<DiscordToken>(await response.Content.ReadAsStringAsync());
        }

        private async Task<DiscordUser> _UserRequest(DiscordToken discordToken)
        {
            // Request a user profile using access token
            HttpRequestMessage userRequest = new HttpRequestMessage(HttpMethod.Get, _discordUserURI);
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", discordToken.access_token);

            var httpUserResponse = await this.client.SendAsync(userRequest);

            return JsonSerializer.Deserialize<DiscordUser>(await httpUserResponse.Content.ReadAsStringAsync());
        }
    }
}