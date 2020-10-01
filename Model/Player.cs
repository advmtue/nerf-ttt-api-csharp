using System.Reflection.Emit;
using System.Text.Json.Serialization;
using csharp_api.Model.Roles;

namespace csharp_api.Model.Player
{
    public abstract class Player
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        public Player(Player player)
        {
            UserId = player.UserId;
            DisplayName = player.DisplayName;
        }

        public Player() { }
    }

    public class LobbyPlayer : Player
    {
        [JsonPropertyName("isReady")]
        public bool IsReady { get; set; }

        // Baseclass constructor
        public LobbyPlayer(Player player) : base(player) { }
        public LobbyPlayer() { }
    }

    public class GamePlayer : Player
    {
        [JsonPropertyName("isAlive")]
        public bool IsAlive { get; set; }

        [JsonIgnore]
        public Role Role { get; set; }

        [JsonPropertyName("role")]
        public string RoleName { get => Roles.RoleInfo.GetName(Role); }

        [JsonPropertyName("killerId")]
        public string KillerId { get; set; }

        [JsonPropertyName("killTime")]
        public string KillTime { get; set; }

        [JsonPropertyName("analyzerCode")]
        public string AnalyzerCode { get; set; }

        // Detective only
        [JsonPropertyName("scansRemaining")]
        public int ScansRemaining { get; set; }

        [JsonPropertyName("lastScanTime")]
        public string LastScanTime { get; set; }

        // Baseclass constructor
        public GamePlayer(Player player) : base(player) { }

        public GamePlayer() { }
    }

    public class GamePlayerBasic : Player
    {
        [JsonIgnore]
        public Role Role { get; set; }

        [JsonPropertyName("role")]
        public string RoleName { get => RoleInfo.GetName(Role); }
        public GamePlayerBasic(Player player) : base(player) { }

        public GamePlayerBasic() : base() { }

        public GamePlayerBasic(string userId, string displayName, Role role)
        {
            UserId = userId;
            DisplayName = displayName;
            Role = role;
        }
    }
}