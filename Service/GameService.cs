using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Database;
using csharp_api.Model.Game;
using csharp_api.Model.Lobby;

namespace csharp_api.Services
{
    public class PlayerNotInGameException : Exception { }

    public class GameService
    {
        private IDatabase _database;

        // Create a cache for game players, since the information doesn't change
        private Dictionary<string, List<GamePlayer>> _gamePlayerCache = new Dictionary<string, List<GamePlayer>>();

        public GameService(IDatabase database)
        {
            _database = database;
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

            // Filter list
            gamePlayerCopy = gamePlayerCopy.FindAll(player =>
            {
                if (player.Role == "INNOCENT")
                {
                    return false;
                }
                else if (player.UserId == localPlayer.UserId)
                {
                    return false;
                }
                else if (player.Role == localPlayer.Role)
                {
                    return true;
                }
                else if (player.Role == "DETECTIVE")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            });

            List<GamePlayerBasic> gamePlayersBasic = new List<GamePlayerBasic>();
            foreach (GamePlayer player in gamePlayerCopy)
            {
                gamePlayersBasic.Add(new GamePlayerBasic(player.DisplayName, player.Role));
            }

            return new GamePlayerInfo
            {
                Role = localPlayer.Role,
                AnalyzerCode = localPlayer.AnalyzerCode,
                ScansRemaining = localPlayer.ScansRemaining,
                LastScanTime = localPlayer.LastScanTime,
                KnownRoles = gamePlayersBasic
            };
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