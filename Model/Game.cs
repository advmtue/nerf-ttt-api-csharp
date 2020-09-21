using System.Text.Json.Serialization;
using csharp_api.Model.Lobby;

namespace csharp_api.Model.Game
{
    public class GameMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

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
            } else {
                this.ScansRemaining = 0;
            }
        }
    }
}