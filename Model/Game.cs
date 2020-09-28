using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using csharp_api.Model.Player;
using csharp_api.Model.Game.Kill;

namespace csharp_api.Model.Game
{
    public enum Status
    {
        LOBBY,
        PREGAME,
        INGAME,
        POSTPENDING,
        POSTGAME,
    }

    public static class StatusName
    {
        public const string Lobby = "LOBBY";
        public const string Pregame = "PREGAME";
        public const string Ingame = "INGAME";
        public const string PostPending = "POSTPENDING";
        public const string Postgame = "POSTGAME";

        public static string Get(Status status)
        {
            switch (status)
            {
                case Status.LOBBY:
                    return StatusName.Lobby;
                case Status.PREGAME:
                    return StatusName.Pregame;
                case Status.INGAME:
                    return StatusName.Ingame;
                case Status.POSTPENDING:
                    return StatusName.PostPending;
                case Status.POSTGAME:
                    return StatusName.Postgame;
                default:
                    throw new ArgumentException();
            }
        }

        public static Status ToStatus(string status)
        {
            switch (status)
            {
                case StatusName.Lobby:
                    return Status.LOBBY;
                case StatusName.Pregame:
                    return Status.PREGAME;
                case StatusName.Ingame:
                    return Status.INGAME;
                case StatusName.PostPending:
                    return Status.POSTPENDING;
                case StatusName.Postgame: 
                    return Status.POSTGAME;
                default:
                    throw new ArgumentException();
            }
        }
    }

    public class GameInfo
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("ownerId")]
        public string OwnerId { get; set; }

        [JsonPropertyName("ownerName")]
        public string OwnerName { get; set; }

        [JsonPropertyName("status")]
        public string StatusName { get => Game.StatusName.Get(Status); }

        [JsonIgnore]
        public Status Status { get; set; }

        // Timestamps
        [JsonPropertyName("dateLaunched")]
        public string DateLaunched { get; set; }

        [JsonPropertyName("dateStarted")]
        public string DateStarted { get; set; }

        [JsonPropertyName("dateCreated")]
        public string DateCreated { get; set; }

        [JsonPropertyName("dateEnded")]
        public string DateEnded { get; set; }

        [JsonPropertyName("nextGameCode")]
        public string NextGameCode { get; set; }

        [JsonPropertyName("winningTeam")]
        public string WinningTeam { get; set; }

        public GameInfo() { }

        public GameInfo(GameInfo info)
        {
            Code = info.Code;
            OwnerId = info.OwnerId;
            OwnerName = info.OwnerName;
            Status = info.Status;
            DateCreated = info.DateCreated;
            DateLaunched = info.DateLaunched;
            DateStarted = info.DateStarted;
            DateEnded = info.DateEnded;
            NextGameCode = info.NextGameCode;
            WinningTeam = info.WinningTeam;
        }
    }

    public class LobbyInfo : GameInfo
    {
        [JsonPropertyName("lobbyPlayers")]
        public List<LobbyPlayer> Players { get; set; }

        // Baseclass constructor
        public LobbyInfo(GameInfo info) : base(info) { }
    }

    public class PregameInfo : GameInfo
    {
        [JsonPropertyName("gamePlayers")]
        public List<GamePlayerBasic> Players { get; set; }

        [JsonPropertyName("localPlayer")]
        public GamePlayer LocalPlayer { get; set; }

        // Baseclass constructor
        public PregameInfo(GameInfo info) : base(info) { }
    }

    public class IngameInfo : GameInfo
    {
        [JsonPropertyName("gamePlayers")]
        public List<GamePlayerBasic> Players { get; set; }

        [JsonPropertyName("localPlayer")]
        public GamePlayer LocalPlayer { get; set; }

        // Baseclass constructor
        public IngameInfo(GameInfo info) : base(info) { }
    }

    public class PostPendingInfo : GameInfo
    {
        [JsonPropertyName("waitingFor")]
        public List<GamePlayerBasic> WaitingFor { get; set; }

        [JsonPropertyName("gamePlayers")]
        public List<GamePlayerBasic> Players { get; set; }

        // Baseclass constructor
        public PostPendingInfo(GameInfo info) : base(info) { }
    }

    public class PostGameInfo : GameInfo
    {
        [JsonPropertyName("kills")]
        public List<GameKill> GameKills { get; set; }

        // Baseclass constructor
        public PostGameInfo(GameInfo info) : base(info) { }
    }
}