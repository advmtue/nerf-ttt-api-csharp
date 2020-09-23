using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Database;
using csharp_api.Model.Game;
using csharp_api.Model.Lobby;
using csharp_api.Services.Message;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Services
{
    public class PlayerNotInGameException : Exception { }

    public class GameService
    {
        private IDatabase _database;
        private MessageService _messageService;

        // Create a cache for game players, since the information doesn't change
        private Dictionary<string, List<GamePlayer>> _gamePlayerCache = new Dictionary<string, List<GamePlayer>>();

        public GameService(IDatabase database, MessageService messageService)
        {
            _database = database;
            _messageService = messageService;
        }

        public async Task<string> Create(LobbyMetadata lobbyInfo, List<LobbyPlayer> players)
        {
            // Random for allocation of traitors
            var random = new Random();

            // TODO Determine the actual ratios for traitor and detective
            int traitorCount = (players.Count / 6) + 1;
            int detectiveCount = (players.Count / 8) + 1;
            int playerCount = players.Count;

            List<GamePlayer> gamePlayers = new List<GamePlayer>();

            List<string> analyzerList = _generateAnalyzerCodes(players.Count);

            while (gamePlayers.Count != playerCount)
            {
                // Take a random player
                int playerIdx = random.Next(0, players.Count);

                // Get an analyzer code
                string code = analyzerList[0];
                analyzerList.RemoveAt(0);

                string role = "INNOCENT";

                if (traitorCount > 0)
                {
                    // Assign to traitor
                    role = "TRAITOR";
                    traitorCount--;
                }
                else if (detectiveCount > 0)
                {
                    // Assign to detective
                    role = "DETECTIVE";
                    detectiveCount--;
                }

                GamePlayer ply = new GamePlayer(players[playerIdx], role, code);
                gamePlayers.Add(ply);
            }


            GameMetadata gameInfo = await _database.GameCreate(lobbyInfo, gamePlayers);

            // Cache player information
            _gamePlayerCache.Add(gameInfo.GameId, gamePlayers);

            return gameInfo.GameId;
        }

        public async Task<GameMetadata> Get(string gameId)
        {
            return await _database.GetGameById(gameId);
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
                gamePlayers = await _database.GetGamePlayers(gameId);
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
            await _database.GameStart(gameId, callingPlayerId);
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
            var playerList = await _database.GetGamePlayers(gameId);
            var winningTeam = _calculateWinningTeamTimer(playerList);

            try {
                await _database.GameEndTimer(gameId, winningTeam);
                await _messageService.GameEndTimer(gameId);
            } catch (ConditionalCheckFailedException)
            {
                Console.WriteLine("[GameService] Game timer ended but game is not INGAME --IGNORING--");
            }
        }

        private static string _calculateWinningTeamTimer(List<GamePlayer> players) 
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
        private static List<string> _generateAnalyzerCodes(int length)
        {
            var random = new Random();
            const string letters = "ABCDEFGHJKLMNOPQRSTUVWXYZ2345678";

            List<string> codes = new List<string>();

            while (codes.Count < length)
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
    }
}