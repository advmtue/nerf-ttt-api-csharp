using System.Text.Json.Serialization;
using csharp_api.Model.Player;
using csharp_api.Model.Roles;

namespace csharp_api.Model.Game.Kill
{
    public class GameKill {
        [JsonPropertyName("killerName")]
        public string KillerName { get; set; }

        [JsonPropertyName("killerId")]
        public string KillerId { get; set; }

        [JsonIgnore]
        public Role KillerRole { get; set; }

        [JsonPropertyName("killerRole")]
        public string KillerRoleName { get => RoleInfo.GetName(KillerRole); }


        [JsonPropertyName("victimName")]
        public string VictimName { get; set; }

        [JsonPropertyName("victimId")]
        public string VictimId { get; set; }

        [JsonIgnore]
        public Role VictimRole { get; set; }

        [JsonPropertyName("victimRole")]
        public string VictimRoleName { get => RoleInfo.GetName(VictimRole); }

        [JsonPropertyName("killTime")]
        public string Time { get; set; }


        public GameKill() {}
    }
}