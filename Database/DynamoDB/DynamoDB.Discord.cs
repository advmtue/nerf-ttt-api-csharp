using System;
using System.Threading.Tasks;
using csharp_api.Model.User;
using csharp_api.Model.User.Discord;
using csharp_api.Transfer.Response.Discord;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Database.DynamoDB
{
    public partial class DynamoDBContext : IDatabase
    {
        // Todo split into partial
        public async Task<Profile> GetUserByDiscord(DiscordUser discordUser)
        {
            // Create a GetItemRequest for User
            GetItemRequest getDiscordLoginRequest = DiscordLogin.BuildGetRequest(discordUser.id);
            getDiscordLoginRequest.TableName = this._tableName;

            // Perform response
            GetItemResponse discordLoginResponse;
            try
            {
                discordLoginResponse = await this._client.GetItemAsync(getDiscordLoginRequest);
            }
            catch (Exception ex)
            {
                // TODO Handle database exceptions
                Console.WriteLine("[Database] Failed to pull discord user");
                throw ex;
            }

            // User doesn't exist
            if (!discordLoginResponse.IsItemSet)
            {
                return null;
            }

            // Extract userId
            string userId = discordLoginResponse.Item["userId"].S;

            // Return the user
            return await GetUserById(userId);
        }

        public async Task<Profile> CreateUserByDiscord(DiscordUser discordUser)
        {
            // Goal: Create a registration-level user with a 5 minute TTL

            string userId = Guid.NewGuid().ToString();

            // Create a new discord login
            DiscordLogin newLogin = new DiscordLogin()
            {
                UserId = userId.ToString(),
                DiscordId = discordUser.id,
            };

            // Create a sparse profile
            Profile newProfile = new Profile()
            {
                UserId = userId,
                AccessLevel = "registration",
                DisplayName = "Unregistered User",
            };

            PutItemRequest discordPutRequest = newLogin.BuildPutRequest();
            PutItemRequest profilePutRequest = newProfile.BuildPutRequest();

            // Assign table name
            discordPutRequest.TableName = _tableName;
            profilePutRequest.TableName = _tableName;

            try
            {
                await _client.PutItemAsync(discordPutRequest);
                await _client.PutItemAsync(profilePutRequest);
            }
            catch (Exception)
            {
                Console.WriteLine("[Database] Failed to insert discord login or profile.");
                throw new DefaultDatabaseException();
            }

            return newProfile;
        }
    }
}