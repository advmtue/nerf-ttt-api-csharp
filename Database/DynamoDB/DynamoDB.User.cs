using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;

using csharp_api.Model.User;

namespace csharp_api.Database.DynamoDB
{
    public partial class DynamoDBContext : IDatabase
    {
        public async Task<Profile> GetUserById(string userId)
        {
            GetItemRequest profileRequest = Profile.BuildGetRequest(userId);
            profileRequest.TableName = _tableName;

            GetItemResponse profileResponse;
            try
            {
                profileResponse = await _client.GetItemAsync(profileRequest);
            }
            catch (Exception)
            {
                Console.WriteLine("[Database] Failed to lookup user by ID");
                throw new DefaultDatabaseException();
            }

            if (!profileResponse.IsItemSet)
            {
                return null;
            }
            else
            {
                return Profile.CreateFromItem(profileResponse.Item);
            }
        }

        public async Task RegisterUser(string userId, string name)
        {
            UpdateItemRequest nameUpdateRequest = new UpdateItemRequest()
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
            };

            try
            {
                await _client.UpdateItemAsync(nameUpdateRequest);
            }
            catch (Exception)
            {
                Console.WriteLine("[Database] Failed to update user registration status");
                throw new DefaultDatabaseException();
            }
        }
    }
}