using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;

namespace csharp_api.Model.User.Discord
{
    public class DiscordLogin
    {
        public string DiscordId { get; set; }
        public string UserId { get; set; }

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