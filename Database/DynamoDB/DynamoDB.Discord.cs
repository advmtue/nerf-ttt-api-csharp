using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using csharp_api.Model.User;
using csharp_api.Model.User.Discord;
using csharp_api.Transfer.Response.Discord;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Database.DynamoDB
{
    public partial class DynamoDBContext : IDatabase
    {
        public async Task<Profile> GetUserByDiscord(DiscordUser discordUser)
        {
            // Attempt to pull a discord login
            GetItemResponse discordLoginResponse = await _client.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                   { "pk", new AttributeValue { S = $"DISCORD#{discordUser.id}" } },
                   { "sk", new AttributeValue { S = "login" } }
               }
            });

            // User doesn't exist
            if (!discordLoginResponse.IsItemSet)
            {
                throw new UserNotFoundException();
            }

            // Return the user
            try
            {
                return await GetUserById(discordLoginResponse.Item["userId"].S);
            }
            catch (UserNotFoundException)
            {
                // Discord exists but not user profile? This is bad.
                // FIXME Oh no
                Console.WriteLine($"[Database] Discord User exists, but profile doesnt! DiscordID = {discordUser.id}");
                throw new DefaultDatabaseException();
            }
        }

        public async Task<Profile> CreateUserByDiscord(DiscordUser discordUser)
        {
            // Generate a brand new user id
            string userId = Guid.NewGuid().ToString();

            // Put the discord profile
            Task putDiscordProfile = _client.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"DISCORD#{discordUser.id}" } },
                    { "sk", new AttributeValue { S = "login" } },
                    { "userId", new AttributeValue { S = userId } }
                },
                ConditionExpression = "attribute_not_exists(pk)"
            });

            // Put the user profile
            Task putUserProfile = _client.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue($"USER#{userId}") },
                    { "sk", new AttributeValue("profile") },
                    { "GSI1-SK", new AttributeValue("Unregistered User") },
                    { "GSI1-PK", new AttributeValue("user") },
                    { "accessLevel", new AttributeValue("registration") },
                    { "joinDate", new AttributeValue { N = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() } }
                },
                ConditionExpression = "attribute_not_exists(pk)"
            });

            // Perform insertions
            Task.WaitAll(putDiscordProfile, putUserProfile);

            Profile newProfile = new Profile
            {
                UserId = userId,
                AccessLevel = "registration",
                DisplayName = "Unregistered User",
            };

            return newProfile;
        }
    }
}