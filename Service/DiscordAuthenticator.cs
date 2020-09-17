using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

using csharp_api.Database;
using csharp_api.Model.User;
using csharp_api.Model.User.Discord;
using csharp_api.Transfer.Response.Discord;

namespace csharp_api.Services.Discord
{
    public class DiscordAuthenticator : IAuthenticationService<Profile>
    {
        private readonly string client_id;
        private readonly string client_secret;
        private readonly HttpClient client = new HttpClient();
        private readonly string discordTokenURI;
        private readonly string discordUserURI;
        private readonly IDatabase _database;

        public DiscordAuthenticator(IDatabase database)
        {
            _database = database;

            // Todo Configuration or injection (or both)
            this.client_id = "754956360424226882";
            this.client_secret = "n_dmzWF57NzbjPro5tN1DmqnnjeBg6NB";
            this.discordTokenURI = "https://discord.com/api/oauth2/token";
            this.discordUserURI = "https://discord.com/api/users/@me";
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

                if (profile == null)
                {
                    // Create a new user
                    profile = await _database.CreateUserByDiscord(discordUser);
                }
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
            requestBody.Add("client_id", this.client_id);
            requestBody.Add("client_secret", this.client_secret);
            requestBody.Add("grant_type", "authorization_code");
            requestBody.Add("code", code);
            requestBody.Add("redirect_uri", "http://localhost:4200/auth?ref=discord");
            requestBody.Add("scope", "identify");

            // Encode the body content
            var bodyContent = new FormUrlEncodedContent(requestBody);

            // Build the request
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, this.discordTokenURI)
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
            HttpRequestMessage userRequest = new HttpRequestMessage(HttpMethod.Get, discordUserURI);
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", discordToken.access_token);

            var httpUserResponse = await this.client.SendAsync(userRequest);

            return JsonSerializer.Deserialize<DiscordUser>(await httpUserResponse.Content.ReadAsStringAsync());
        }
    }
}