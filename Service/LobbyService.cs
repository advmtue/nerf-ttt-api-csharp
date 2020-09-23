using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using csharp_api.Database;

using csharp_api.Model.User;
using csharp_api.Model.Lobby;
using csharp_api.Controllers;
using csharp_api.Services.Message;

namespace csharp_api.Services
{
    public class CodePoolExhaustedException : Exception { }
    public class PlayerNotOwnerException : Exception { }
    public class LobbyNotStartableException : Exception { }
    public class MinimumPlayersException : Exception { }
    public class PlayersNotReadyException : Exception { }

    // TODO Create interface
    public class LobbyService
    {

        private IDatabase _database;
        private List<string> _usedLobbyCodes = new List<string>();
        private MessageService _messageService;
        private GameService _gameService;

        public LobbyService(IDatabase database, MessageService messageService, GameService gameService)
        {
            _database = database;
            _messageService = messageService;
            _gameService = gameService;
        }

        public static string GenerateCode(int length)
        {
            var random = new Random();
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            return new string(Enumerable.Repeat(letters, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task<LobbyMetadata> Create(NewLobbyRequest request, string ownerId)
        {
            // Lookup the owner
            Profile ownerProfile = await _database.GetUserById(ownerId);

            Console.WriteLine("[LobbyService] Generating new lobby code");

            string code = "default";
            bool available = false;
            int attempts = 0;
            while (!available && attempts < 1000)
            {
                code = GenerateCode(5);

                if (!_usedLobbyCodes.Contains(code))
                {
                    available = true;
                }
                else
                {
                    Console.WriteLine("[LobbyService] Ignored code with collision = " + code);
                    attempts++;
                }
            }

            // Couldn't find a valid code in the pool
            if (code == "default")
            {
                throw new CodePoolExhaustedException();
            }

            // Found a valid code, add it to the list of used codes
            Console.WriteLine($"[LobbyService] Code = {code}, Attempts = {attempts}");
            _usedLobbyCodes.Add(code);

            // Build a lobby object
            LobbyMetadata lobbyInfo = new LobbyMetadata(ownerProfile, request.name, code);

            // Create a lobby
            await _database.CreateLobby(lobbyInfo);

            return lobbyInfo;
        }

        public async Task<string> Start(string code, string callingUserId)
        {
            LobbyMetadata lobbyMeta = await _database.GetLobbyByCode(code);

            // Check the owner of the lobby is the calling player
            if (lobbyMeta.OwnerId != callingUserId)
            {
                throw new PlayerNotOwnerException();
            }

            // Check the lobby status is LOBBY
            if (lobbyMeta.Status != "LOBBY")
            {
                throw new LobbyNotStartableException();
            }

            // Check that there is at least 3 players
            if (lobbyMeta.PlayerCount < 0)
            {
                throw new MinimumPlayersException();
            }

            List<LobbyPlayer> players = await _database.LobbyGetPlayers(code);

            // Check that all players are ready
            var potentialUnreadyPlayer = players.Find(p => !p.IsReady);
            if (potentialUnreadyPlayer != null)
            {
                throw new PlayersNotReadyException();
            }

            string newGameId = await _gameService.Create(lobbyMeta, players);

            // Let players know the lobby has launched
            await _messageService.LobbyLaunch(code, newGameId);

            return newGameId;
        }

        public async Task<LobbyMetadata> GetByCode(string code)
        {
            return await _database.GetLobbyByCode(code);
        }

        public async Task CloseLobbyByAdmin(string code)
        {
            await _database.LobbyCloseByAdmin(code);
            await _messageService.LobbyClose(code);

            this._usedLobbyCodes.Remove(code);
        }

        public async Task<List<LobbyPlayer>> GetLobbyPlayers(string lobbyCode)
        {
            return await _database.LobbyGetPlayers(lobbyCode);
        }

        public async Task PlayerJoinLobby(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            Profile userProfile = await _database.GetUserById(userId);
            await _database.LobbyPlayerJoin(lobbyCode, userProfile);
            await _messageService.LobbyPlayerJoin(lobbyCode, userProfile);
        }

        public async Task PlayerLeaveLobby(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.LobbyPlayerLeave(lobbyCode, userId);
            await _messageService.LobbyPlayerLeave(lobbyCode, userId);
        }

        public async Task PlayerSetReady(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.LobbyPlayerSetReady(lobbyCode, userId);
            await _messageService.LobbyPlayerReady(lobbyCode, userId);
        }

        public async Task PlayerSetUnready(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.LobbyPlayerSetUnready(lobbyCode, userId);
            await _messageService.LobbyPlayerUnready(lobbyCode, userId);
        }
    }
}