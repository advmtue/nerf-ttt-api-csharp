using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;
using csharp_api.Model.User;

namespace csharp_api.Model.Game
{
    public class GameMetadata
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("dateCreated")]
        public string DateCreated { get; set; }

        [JsonPropertyName("dateLaunched")]
        public string DateLaunched { get; set; }

        [JsonPropertyName("dateStarted")]
        public string DateStarted { get; set; }

        [JsonPropertyName("dateEnded")]
        public string DateEnded { get; set; }

        [JsonPropertyName("ownerId")]
        public string OwnerId { get; set; }

        [JsonPropertyName("ownerName")]
        public string OwnerName { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("winningTeam")]
        public string WinningTeam { get; set; }

        [JsonPropertyName("nextGameCode")]
        public string NextGameCode { get; set; }

        public GameMetadata() { }

        // Create a new Game using an ownerProfile and code
        public GameMetadata(Profile ownerProfile, string code)
        {
            this.Code = code;
            this.DateCreated = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            this.OwnerName = ownerProfile.DisplayName;
            this.OwnerId = ownerProfile.UserId;
            this.Status = "LOBBY";
        }

        // Marshall from a database query
        public GameMetadata(Dictionary<string, AttributeValue> item)
        {
            this.Code = item["pk"].S.Split("#")[1];
            this.DateCreated = item["dateCreated"].N;
            this.OwnerId = item["GSI1-SK"].S;
            this.OwnerName = item["ownerName"].S;
            this.Status = item["GSI1-PK"].S;

            // May be null
            // TODO See if there's a cleaner way of retrieving this
            this.DateLaunched = item.ContainsKey("dateLaunched") ? item["dateLaunched"].N : null;
            this.DateStarted = item.ContainsKey("dateStarted") ? item["dateStarted"].N : null;
            this.DateEnded = item.ContainsKey("dateEnded") ? item["dateEnded"].N : null;
            this.WinningTeam = item.ContainsKey("winningTeam") ? item["winningTeam"].S : null;
            this.NextGameCode = item.ContainsKey("nextGameCode") ? item["nextGameCode"].S : null;
        }
    }

    public class GamePlayer
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("isAlive")]
        public bool IsAlive { get; set; }

        [JsonPropertyName("analyzerCode")]
        public string AnalyzerCode { get; set; }

        [JsonPropertyName("scansRemaining")]
        public int ScansRemaining { get; set; }

        [JsonPropertyName("lastScanTime")]
        public string LastScanTime { get; set; }

        [JsonPropertyName("ready")]
        public bool IsReady { get; set; }

        public GamePlayer() { }

        public GamePlayer Clone()
        {
            return new GamePlayer
            {
                UserId = this.UserId,
                DisplayName = this.DisplayName,
                Role = this.Role,
                IsAlive = this.IsAlive,
                AnalyzerCode = this.AnalyzerCode,
                ScansRemaining = this.ScansRemaining,
                LastScanTime = this.LastScanTime
            };
        }

        public GamePlayer(Dictionary<string, AttributeValue> item)
        {
            this.UserId = item["sk"].S.Split("#")[1];
            this.DisplayName = item["displayName"].S;
            this.IsReady = item["ready"].BOOL;

            // Potentially null
            this.AnalyzerCode = item.ContainsKey("analyzerCode") ? item["analyzerCode"].S : null;
            this.Role = item.ContainsKey("role") ? item["role"].S : null;
            this.IsAlive = item.ContainsKey("alive") ? item["alive"].BOOL : false;
            this.ScansRemaining = item.ContainsKey("scansRemaining") ? Int32.Parse(item["scansRemaining"].N) : 0;
            this.LastScanTime = item.ContainsKey("lastScanTime") ? item["lastScanTime"].N : null;
        }
    }

    // Simple DTO for player display
    // TODO Add user id
    public class GamePlayerBasic
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        public GamePlayerBasic(string userId, string name, string role)
        {
            UserId = userId;
            Name = name;
            Role = role;
        }
    }

    // DTO for a localPlayer
    public class GamePlayerInfo
    {

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("analyzerCode")]
        public string AnalyzerCode { get; set; }

        [JsonPropertyName("scansRemaining")]
        public int ScansRemaining { get; set; }

        [JsonPropertyName("lastScanTime")]
        public string LastScanTime { get; set; }

        [JsonPropertyName("players")]
        public List<GamePlayerBasic> Players { get; set; }

        [JsonPropertyName("alive")]
        public bool Alive { get; set; }

        public GamePlayerInfo() { }
    }
}