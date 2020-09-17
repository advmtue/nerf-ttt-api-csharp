using System.Collections.Generic;
using System.Linq;
using System;
using csharp_api.Model.User;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Model.Lobby
{
    public class Metadata
    {
        public string code { get; set; }
        public string name { get; set; }
        public long dateCreated { get; set; }
        public int roundCount { get; set; }
        public int playerCount { get; set; }
        public string ownerName { get; set; }
        public string ownerId { get; set; }
        public string status { get; set; }

        public Metadata(Profile ownerProfile, string name, string code)
        {
            this.code = code;
            this.name = name;
            this.dateCreated = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            this.roundCount = 0;
            this.playerCount = 0;
            this.ownerName = ownerProfile.DisplayName;
            this.ownerId = ownerProfile.UserId;
            this.status = "LOBBY";
        }

        // Create from a query response
        public Metadata(Dictionary<string, AttributeValue> item)
        {
            this.code = item["pk"].S.Split("#")[1];
            this.name = item["name"].S;
            this.dateCreated = Int64.Parse(item["dateCreated"].N);
            this.roundCount = Int32.Parse(item["roundCount"].N);
            this.playerCount = Int32.Parse(item["playerCount"].N);
            this.ownerId = item["GSI1-SK"].S;
            this.ownerName = item["ownerName"].S;
            this.status = item["GSI1-PK"].S;
        }
    }

    public class LobbyPlayer
    {
        public string userId { get; set; }
        public string displayName { get; set; }
        public bool ready { get; set; }

        public LobbyPlayer(Dictionary<string, AttributeValue> item)
        {
            this.userId = item["userId"].S;
            this.displayName = item["displayName"].S;
            this.ready = item["ready"].BOOL;
        }
    }
}