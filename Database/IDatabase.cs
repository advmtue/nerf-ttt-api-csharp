using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using csharp_api.Transfer.Response.Discord;
using csharp_api.Model.User;
using csharp_api.Model.Lobby;
using csharp_api.Model.Game;

namespace csharp_api.Database
{
    public class DefaultDatabaseException : Exception {}
    public class UserNotFoundException : Exception {}
    public class LobbyNotFoundException : Exception {}
    public class GameNotFoundException : Exception {}


    public interface IDatabase
    {
        Task<Profile> GetUserByDiscord(DiscordUser discordUser);
        Task<Profile> CreateUserByDiscord(DiscordUser discordUser);
        Task<Profile> GetUserById(string userId);
        Task RegisterUser(string userId, string name);
        Task CreateLobby(LobbyMetadata lobbyInfo);
        Task<LobbyMetadata> GetLobbyByCode(string lobbyCode);
        Task LobbyCloseByAdmin(string lobbyCode);
        Task<List<LobbyPlayer>> LobbyGetPlayers(string lobbyCode);
        Task LobbyPlayerJoin(string lobbyCode, Profile userProfile);
        Task LobbyPlayerLeave(string lobbyCode, string userId);
        Task LobbyPlayerSetReady(string lobbyCode, string userId);
        Task LobbyPlayerSetUnready(string lobbyCode, string userId);
        Task<GameMetadata> GameCreate(LobbyMetadata lobbyInfo, List<GamePlayer> players);
        Task<GameMetadata> GetGameById(string gameId);
        Task<List<GamePlayer>> GetGamePlayers(string gameId);
    }
}