using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Database;
using csharp_api.Model.Game;
using csharp_api.Model.User;
using csharp_api.Services.Message;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Services
{
    public class PlayerNotInGameException : Exception { }
    public class CodePoolExhaustedException : Exception { }
    public class PlayerNotOwnerException : Exception { }
    public class LobbyNotStartableException : Exception { }
    public class MinimumPlayersException : Exception { }
    public class PlayersNotReadyException : Exception { }

    public class GameService
    {
        private IDatabase _database;
        private MessageService _messageService;
        private List<string> _usedGameCodes = new List<string>();

        // Create a cache for game players, since the information doesn't change
        private Dictionary<string, List<GamePlayer>> _gamePlayerCache = new Dictionary<string, List<GamePlayer>>();

        public GameService(IDatabase database, MessageService messageService)
        {
            _database = database;
            _messageService = messageService;
        }

        public void _InitializePlayers(List<GamePlayer> players)
        {
            // Random for allocation of traitors
            var random = new Random();

            // TODO Determine the actual ratios for traitor and detective
            int traitorCount = (players.Count / 6) + 1;
            int detectiveCount = (players.Count / 8) + 1;
            int playerCount = players.Count;

            List<string> analyzerList = _GenerateAnalyzerCodes(players.Count);
            List<int> indexPool = Enumerable.Range(0, players.Count).ToList();

            while (analyzerList.Count != 0)
            {
                // Take a random player
                int indexPoolIdx = random.Next(0, indexPool.Count);
                int playerIdx = indexPool[indexPoolIdx];
                indexPool.RemoveAt(indexPoolIdx);

                GamePlayer ply = players[playerIdx];

                // Set analyzer code
                ply.LastScanTime = "0";
                ply.ScansRemaining = 0;
                ply.AnalyzerCode = analyzerList[0];
                analyzerList.RemoveAt(0);

                // Default role = INNOCENT
                ply.Role = "INNOCENT";

                // Set player to alive
                ply.IsAlive = true;

                if (traitorCount > 0)
                {
                    // Assign to traitor
                    ply.Role = "TRAITOR";

                    traitorCount--;
                }
                else if (detectiveCount > 0)
                {
                    // Assign to detective
                    ply.Role = "DETECTIVE";
                    ply.ScansRemaining = 3;

                    detectiveCount--;
                }
            }
        }

        public async Task<GameMetadata> Get(string gameId)
        {
            return await _database.GetGame(gameId);
        }

        public async Task<GamePlayerInfo> GetFilteredInfo(string gameId, string userId)
        {
            // Check cache for player list
            List<GamePlayer> gamePlayers;

            if (_gamePlayerCache.ContainsKey(gameId))
            {
                gamePlayers = _gamePlayerCache[gameId];
            }
            else
            {
                // Pull from DB
                gamePlayers = await _database.GameGetPlayers(gameId);
                _gamePlayerCache.Add(gameId, gamePlayers);
            }

            // Clone the list so we don't accidentally make modifications
            List<GamePlayer> gamePlayerCopy = gamePlayers.Select(pl => pl.Clone()).ToList();

            // Determine the current player role
            GamePlayer localPlayer = gamePlayerCopy.Find(player => player.UserId == userId);

            // Make sure the calling player is actually in the game
            if (localPlayer == null)
            {
                throw new PlayerNotInGameException();
            }

            // TODO Create classes for roles with static methods to handle canSeeRole
            // also filters out localPlayer
            List<GamePlayerBasic> gamePlayersBasic = gamePlayerCopy.FindAll(p => p.UserId != localPlayer.UserId)
            .Select(player =>
            {
                if (player.UserId == localPlayer.UserId)
                {
                    // Localplayer can always see their own role
                }
                else if (player.Role == localPlayer.Role)
                {
                    // Localplayer can see their own team
                }
                else if (player.Role == "DETECTIVE")
                {
                    // Everyone can see detective
                }
                else
                {
                    player.Role = "INNOCENT";
                }

                return new GamePlayerBasic(player.DisplayName, player.Role);
            }).ToList();


            return new GamePlayerInfo
            {
                Role = localPlayer.Role,
                AnalyzerCode = localPlayer.AnalyzerCode,
                ScansRemaining = localPlayer.ScansRemaining,
                LastScanTime = localPlayer.LastScanTime,
                KnownRoles = gamePlayersBasic
            };
        }

        public async Task StartGame(string gameId, string callingPlayerId)
        {
            // Attempt to start game (ownerId can be checked as condition)
            await _database.StartGame(gameId, callingPlayerId);
            await _messageService.GameStart(gameId);

            // End the game after some amount of time
            _ = Task.Run(async delegate
            {
                // TODO Configurable
                // Game lasts 10 minutes
                await Task.Delay(600000);
                Console.WriteLine("[GameService] Game would finish now");
                await EndGameTimer(gameId);
            });
        }

        public async Task<List<GamePlayer>> GetGamePlayers(string gameId)
        {
            return await _database.GameGetPlayers(gameId);
        }

        private async Task EndGameTimer(string gameId)
        {
            var playerList = await _database.GameGetPlayers(gameId);
            var winningTeam = _CalculateWinningTeamTimer(playerList);

            try
            {
                await _database.EndGameByTime(gameId, winningTeam);
                await _messageService.GameEndTimer(gameId);
            }
            catch (ConditionalCheckFailedException)
            {
                Console.WriteLine("[GameService] Game timer ended but game is not INGAME --IGNORING--");
            }
        }

        private static string _CalculateWinningTeamTimer(List<GamePlayer> players)
        {
            int numDetectiveAlive = 0;

            foreach (GamePlayer player in players)
            {
                if (player.Role == "DETECTIVE" && player.IsAlive)
                {
                    numDetectiveAlive++;
                }
            }

            return (numDetectiveAlive > 0) ? "INNOCENT" : "TRAITOR";
        }

        private static List<string> _GenerateAnalyzerCodes(int count)
        {
            var random = new Random();
            const string letters = "ABCDEFGHJKLMNOPQRSTUVWXYZ2345678";

            List<string> codes = new List<string>();

            while (codes.Count < count)
            {
                var code = new string(Enumerable.Repeat(letters, 4).Select(s => s[random.Next(s.Length)]).ToArray());

                if (codes.Contains(code))
                {
                    continue;
                }
                else
                {
                    codes.Add(code);
                }
            }

            return codes;
        }

        private static string _GenerateCode(int length)
        {
            var random = new Random();
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            return new string(Enumerable.Repeat(letters, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task<GameMetadata> Create(string ownerId)
        {
            Console.WriteLine("[GameService] Generating new lobby code");

            string code = "default";
            bool available = false;
            int attempts = 0;
            do
            {
                code = _GenerateCode(5);

                if (!_usedGameCodes.Contains(code))
                {
                    available = true;
                }
                else
                {
                    Console.WriteLine("[GameService] Ignored code with collision = " + code);
                }

                attempts++;
            } while (!available && attempts < 1000);

            // Couldn't find a valid code in the pool
            if (code == "default")
            {
                throw new CodePoolExhaustedException();
            }

            // Lookup the owner
            // TODO Ensure that the caller is catching UserNotFoundException
            Profile ownerProfile = await _database.GetUser(ownerId);

            // Found a valid code, add it to the list of used codes
            Console.WriteLine($"[GameService] Code = {code}, Attempts = {attempts}");
            _usedGameCodes.Add(code);

            GameMetadata gameInfo = new GameMetadata(ownerProfile, code);

            // Save
            await _database.CreateGame(gameInfo);

            return gameInfo;
        }

        public async Task Launch(string code, string callingUserId)
        {
            GameMetadata lobbyMeta = await _database.GetGame(code);

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

            List<GamePlayer> players = await _database.GameGetPlayers(code);

            // Check that there is at least 3 players
            // FIXME Remove debug 0
            if (players.Count < 0)
            {
                throw new MinimumPlayersException();
            }

            // Check that all players are ready
            var potentialUnreadyPlayer = players.Find(p => !p.IsReady);
            if (potentialUnreadyPlayer != null)
            {
                throw new PlayersNotReadyException();
            }

            // Allocate roles
            _InitializePlayers(players);

            // Cache players
            _gamePlayerCache.Add(code, players);

            // Save information
            await _database.LaunchGame(code, callingUserId, players);

            // Let players know the lobby has launched
            await _messageService.GameLaunch(code);
        }

        public async Task AdminCloseGame(string code)
        {
            await _database.AdminCloseGame(code);
            await _messageService.GameClose(code);

            this._usedGameCodes.Remove(code);
        }

        public async Task PlayerJoin(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            Profile userProfile = await _database.GetUser(userId);
            await _database.GamePlayerJoin(lobbyCode, userProfile);
            await _messageService.PlayerJoin(lobbyCode, userProfile);
        }

        public async Task PlayerLeave(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.GamePlayerLeave(lobbyCode, userId);
            await _messageService.PlayerLeave(lobbyCode, userId);
        }

        public async Task PlayerSetReady(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.GamePlayerSetReady(lobbyCode, userId);
            await _messageService.PlayerSetReady(lobbyCode, userId);
        }

        public async Task PlayerSetUnready(string lobbyCode, string userId)
        {
            // TODO Check lobby status
            await _database.GamePlayerSetUnready(lobbyCode, userId);
            await _messageService.PlayerSetUnready(lobbyCode, userId);
        }
    }
}