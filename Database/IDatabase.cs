using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using csharp_api.Transfer.Response.Discord;
using csharp_api.Model.User;
using csharp_api.Model.Game;
using csharp_api.Model.Player;
using csharp_api.Model.Game.Kill;

namespace csharp_api.Database
{
    public class DefaultDatabaseException : Exception {}
    public class UserNotFoundException : Exception {}
    public class LobbyNotFoundException : Exception {}
    public class GameNotFoundException : Exception {}

    public interface IDatabase
    {
        // Registration
        Task<Profile> CreateUserByDiscord(DiscordUser discordUser);
        Task RegisterUser(string userId, string name);
        Task<Profile> GetUser(string userId);
        Task<Profile> GetUserByDiscord(DiscordUser discordUser);

        // Get game information
        Task<GameInfo> GetGameInfo(string gameCode);
        Task<List<GamePlayer>> GameGetPlayers(string gameCode);
        Task<List<LobbyPlayer>> GetLobbyPlayers(string gameCode);
        Task<GamePlayer> GameGetPlayer(string gameCode, string userId);

        // Game updates
        Task CreateGame(string gameCode, Profile ownerProfile);
        Task LaunchGame(string gameCode, string callingPlayerId, List<GamePlayer> playerInfo);
        Task StartGame(string gameCode, string callingPlayerId);
        Task EndGamePostPending(string gameCode, string winningTeam);
        Task EndGameComplete(string gameCode);

        // Game actions
        Task AdminCloseGame(string gameCode);
        Task GamePlayerJoin(string gameCode, Profile userProfile);
        Task GamePlayerLeave(string gameCode, string userId);
        Task GamePlayerSetReady(string gameCode, string userId);
        Task GamePlayerSetUnready(string gameCode, string userId);
        Task GamePlayerDie(string gameCode, GamePlayer victim, GamePlayer killer);
        Task GameConfirmKiller(string gameCode, GamePlayer victim, GamePlayer killer);
    }
}