using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;

using csharp_api.Model.User;

namespace csharp_api.Database.DynamoDB
{
    public partial class DynamoDBContext : IDatabase
    {
        public async Task<Profile> GetUserById(string userId)
        {
            GetItemResponse profileResponse = await _client.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue($"USER#{userId}") },
                    { "sk", new AttributeValue("profile") }
                }
            });

            if (!profileResponse.IsItemSet)
            {
                throw new UserNotFoundException();
            }

            return Profile.CreateFromItem(profileResponse.Item);
        }

        public async Task RegisterUser(string userId, string name)
        {
            // Set user display name
            // Set user accessLevel = "user"

            await _client.UpdateItemAsync(new UpdateItemRequest()
            {
                TableName = _tableName,
                Key = new System.Collections.Generic.Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"USER#{userId}"} },
                    { "sk", new AttributeValue() { S = "profile" } }
                },
                UpdateExpression = "SET #displayName = :newDisplayName, accessLevel = :userAccessLevel",
                ConditionExpression = "accessLevel = :registrationAccessLevel",
                ExpressionAttributeNames = new System.Collections.Generic.Dictionary<string, string> {
                    { "#displayName", "GSI1-SK" }
                },
                ExpressionAttributeValues = new System.Collections.Generic.Dictionary<string, AttributeValue> {
                    { ":newDisplayName", new AttributeValue() { S = name } },
                    { ":userAccessLevel", new AttributeValue() { S = "user" } },
                    { ":registrationAccessLevel", new AttributeValue() { S = "registration" } }
                },
            });
        }
    }
}