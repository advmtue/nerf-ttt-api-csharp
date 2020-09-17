using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using csharp_api.Transfer.Response.Discord;
using csharp_api.Model.User;
using csharp_api.Model.Lobby
;

namespace csharp_api.Database
{
    public class DefaultDatabaseException : Exception
    {
        public DefaultDatabaseException() { }
    }

    public class UserNotFoundException : Exception
    {
        public UserNotFoundException() { }
    }

    public class LobbyNotFoundException : Exception
    {
        public LobbyNotFoundException() { }
    }


    public interface IDatabase
    {
        Task<Profile> GetUserByDiscord(DiscordUser discordUser);
        Task<Profile> CreateUserByDiscord(DiscordUser discordUser);
        Task<Profile> GetUserById(string userId);
        Task RegisterUser(string userId, string name);
        Task CreateLobby(Metadata lobbyInfo);
        Task<Metadata> GetLobbyByCode(string lobbyCode);
        Task CloseLobbyByAdmin(string lobbyCode);
        Task<List<LobbyPlayer>> GetLobbyPlayers(string lobbyCode);
        Task PlayerJoinLobby(string lobbyCode, Profile userProfile);
        Task PlayerLeaveLobby(string lobbyCode, string userId);
    }
}