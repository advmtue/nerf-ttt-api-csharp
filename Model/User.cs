using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Model.User
{
    public class Profile
    {
        public string UserId { get; set; }
        public string AccessLevel { get; set; }
        public string DisplayName { get; set; }
        public string JoinDate { get; set; }

        public PutItemRequest BuildPutRequest()
        {
            Dictionary<string, AttributeValue> item = new Dictionary<string, AttributeValue>();
            item.Add("pk", new AttributeValue($"USER#{UserId}"));
            item.Add("sk", new AttributeValue("profile"));
            item.Add("GSI1-PK", new AttributeValue("user"));
            item.Add("GSI1-SK", new AttributeValue(DisplayName));
            item.Add("accessLevel", new AttributeValue(AccessLevel));
            item.Add("joinDate", new AttributeValue() { N = DateTimeOffset.Now.ToUnixTimeSeconds().ToString() });

            return new PutItemRequest() { Item = item };
        }

        public static GetItemRequest BuildGetRequest(string userId)
        {
            Dictionary<string, AttributeValue> key = new Dictionary<string, AttributeValue>() {
                { "pk", new AttributeValue($"USER#{userId}") },
                { "sk", new AttributeValue("profile") },
            };

            return new GetItemRequest() { Key = key };
        }

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