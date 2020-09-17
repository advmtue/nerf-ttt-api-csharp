using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using csharp_api.Database;

using csharp_api.Model.User;
using csharp_api.Model.Lobby;
using csharp_api.Controllers;

namespace csharp_api.Services
{
    public class CodePoolExhaustedException : Exception
    {
        public CodePoolExhaustedException() { }
    }

    // TODO Create interface
    public class LobbyService
    {

        private IDatabase _database;
        private List<string> _usedLobbyCodes = new List<string>();

        public LobbyService(IDatabase database)
        {
            _database = database;
        }

        public static string GenerateCode(int length)
        {
            var random = new Random();
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            return new string(Enumerable.Repeat(letters, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task<Metadata> Create(NewLobbyRequest request, string ownerId)
        {
            // Lookup the owner
            Profile ownerProfile = await _database.GetUserById(ownerId);

            // Ensure the owner exists
            if (ownerProfile == null)
            {
                throw new UserNotFoundException();
            }

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
            Metadata lobbyInfo = new Metadata(ownerProfile, request.name, code);

            // Create a lobby
            await _database.CreateLobby(lobbyInfo);

            return lobbyInfo;
        }

        public async Task<Metadata> GetByCode(string code)
        {
            return await _database.GetLobbyByCode(code);
        }

        public async Task CloseLobbyByAdmin(string code)
        {
            await _database.LobbyCloseByAdmin(code);

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
        }

        public async Task PlayerLeaveLobby(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.LobbyPlayerLeave(lobbyCode, userId);
        }

        public async Task PlayerSetReady(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.LobbyPlayerSetReady(lobbyCode, userId);
        }

        public async Task PlayerSetUnready(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.LobbyPlayerSetUnready(lobbyCode, userId);
        }
    }
}