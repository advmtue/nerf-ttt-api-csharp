using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Model.User
{
    public class Profile
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("accessLevel")]
        public string AccessLevel { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("joinDate")]
        public string JoinDate { get; set; }

        public static Profile CreateFromItem(Dictionary<string, AttributeValue> item)
        {
            Profile profile = new Profile()
            {
                UserId = item["pk"].S.Split("#")[1],
                AccessLevel = item["accessLevel"].S,
                DisplayName = item["GSI1-SK"].S,
                JoinDate = item["joinDate"].N,
            };

            return profile;
        }
    }
}