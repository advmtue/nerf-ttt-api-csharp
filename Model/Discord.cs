using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;

namespace csharp_api.Model.User.Discord
{
    public class DiscordLogin
    {
        public string DiscordId { get; set; }
        public string UserId { get; set; }

        public static GetItemRequest BuildGetRequest(string discordId)
        {
            Dictionary<string, AttributeValue> Key = new Dictionary<string, AttributeValue> {
                { "pk", new AttributeValue { S = $"DISCORD#{discordId}" } },
                { "sk", new AttributeValue { S = "login" } }
            };

            return new GetItemRequest
            {
                Key = Key,
            };
        }

        public PutItemRequest BuildPutRequest()
        {
            return new PutItemRequest()
            {
                Item = new Dictionary<string, AttributeValue>() {
                    { "pk", new AttributeValue($"DISCORD#{this.DiscordId}") },
                    { "sk", new AttributeValue("login") },
                    { "userId", new AttributeValue(UserId) }
                }
            };
        }
    }
}