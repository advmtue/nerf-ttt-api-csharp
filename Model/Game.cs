using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using csharp_api.Model.Lobby;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Model.Game
{
    public class GameMetadata
    {
        [JsonPropertyName("gameId")]
        public string GameId { get; set; }

        [JsonPropertyName("dateLaunched")]
        public string DateLaunched { get; set; }

        [JsonPropertyName("dateStarted")]
        public string DateStarted { get; set; }

        [JsonPropertyName("dateEnded")]
        public string DateEnded { get; set; }

        [JsonPropertyName("winningTeam")]
        public string WinningTeam { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("ownerName")]
        public string OwnerName { get; set; }

        [JsonPropertyName("ownerId")]
        public string OwnerId { get; set; }

        [JsonPropertyName("lobbyCode")]
        public string LobbyCode { get; set; }

        public GameMetadata() { }

        public GameMetadata(Dictionary<string, AttributeValue> item)
        {
            this.GameId = item["pk"].S.Split("#")[1];
            this.DateLaunched = item["dateLaunched"].N;
            this.Status = item["status"].S;
            this.OwnerName = item["ownerName"].S;
            this.OwnerId = item["ownerId"].S;
            this.LobbyCode = item["lobbyCode"].S;

            // May be null
            this.DateStarted = item.ContainsKey("dateStarted") ? item["dateStarted"].N : null;
            this.DateEnded = item.ContainsKey("dateEnded") ? item["dateEnded"].N : null;
            this.WinningTeam = item.ContainsKey("winningTeam") ? item["winningTeam"].S : null;
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

        public GamePlayer() {}

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

        public GamePlayer(LobbyPlayer lobbyPlayer, string role, string analyzerCode)
        {
            this.UserId = lobbyPlayer.UserId;
            this.DisplayName = lobbyPlayer.DisplayName;
            this.Role = role;
            this.IsAlive = true;
            this.AnalyzerCode = analyzerCode;
            this.LastScanTime = "0";

            if (this.Role == "DETECTIVE")
            {
                this.ScansRemaining = 2;
            }
            else
            {
                this.ScansRemaining = 0;
            }
        }

        public GamePlayer(Dictionary<string, AttributeValue> item)
        {
            this.UserId = item["sk"].S.Split("#")[1];
            this.DisplayName = item["displayName"].S;
            this.Role = item["role"].S;
            this.IsAlive = item["isAlive"].BOOL;
            this.AnalyzerCode = item["analyzerCode"].S;
            this.ScansRemaining = item.ContainsKey("scansRemaining") ? Int32.Parse(item["scansRemaining"].N) : 0;
            this.LastScanTime = item.ContainsKey("lastScanTime") ? item["lastScanTime"].S : null;
        }
    }

    public class GamePlayerBasic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        public GamePlayerBasic(string name, string role) {
            Name = name;
            Role = role;
        }
    }

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

        [JsonPropertyName("knownRoles")]
        public List<GamePlayerBasic> KnownRoles { get; set; }

        public GamePlayerInfo() {}
    }
}