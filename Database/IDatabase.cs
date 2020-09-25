using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using csharp_api.Transfer.Response.Discord;
using csharp_api.Model.User;
using csharp_api.Model.Game;

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

        // Get information
        Task<Profile> GetUser(string userId);
        Task<Profile> GetUserByDiscord(DiscordUser discordUser);
        Task<GameMetadata> GetGame(string lobbyCode);
        Task<List<GamePlayer>> GameGetPlayers(string lobbyCode);
        Task<GamePlayer> GameGetPlayer(string lobbyCode, string userId);
        Task<List<GameKill>> GameGetKills(string lobbyCode);

        // Game updates
        Task CreateGame(GameMetadata lobbyInfo);
        Task LaunchGame(string gameId, string callingPlayerId, List<GamePlayer> playerInfo);
        Task StartGame(string gameId, string callingPlayerId);
        Task EndGamePostPending(string gameId, string winningTeam);
        Task EndGameComplete(string gameId);

        // Game actions
        Task AdminCloseGame(string lobbyCode);
        Task GamePlayerJoin(string lobbyCode, Profile userProfile);
        Task GamePlayerLeave(string lobbyCode, string userId);
        Task GamePlayerSetReady(string lobbyCode, string userId);
        Task GamePlayerSetUnready(string lobbyCode, string userId);
        Task GamePlayerDie(string gameId, GamePlayer victim, GamePlayer killer);
        Task GameConfirmKiller(string gameId, GamePlayer victim, GamePlayer killer);
    }
}