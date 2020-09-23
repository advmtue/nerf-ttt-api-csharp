using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using csharp_api.Model.User;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Model.Lobby
{
    public class LobbyMetadata
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("dateCreated")]
        public long DateCreated { get; set; }

        [JsonPropertyName("ownerName")]
        public string OwnerName { get; set; }

        [JsonPropertyName("ownerId")]
        public string OwnerId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("currentGameId")]
        public string CurrentGameId { get; set; }

        public LobbyMetadata(Profile ownerProfile, string code)
        {
            this.Code = code;
            this.DateCreated = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            this.OwnerName = ownerProfile.DisplayName;
            this.OwnerId = ownerProfile.UserId;
            this.Status = "LOBBY";
            this.CurrentGameId = null;
        }

        // Create from a query response
        public LobbyMetadata(Dictionary<string, AttributeValue> item)
        {
            this.Code = item["pk"].S.Split("#")[1];
            this.DateCreated = Int64.Parse(item["dateCreated"].N);
            this.OwnerId = item["GSI1-SK"].S;
            this.OwnerName = item["ownerName"].S;
            this.Status = item["GSI1-PK"].S;
            this.CurrentGameId = item.ContainsKey("currentGameId") ? item["currentGameId"].S : null;
        }
    }

    public class LobbyPlayer
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("ready")]
        public bool IsReady { get; set; }

        public LobbyPlayer(Dictionary<string, AttributeValue> item)
        {
            this.UserId = item["userId"].S;
            this.DisplayName = item["displayName"].S;
            this.IsReady = item["ready"].BOOL;
        }
    }
}