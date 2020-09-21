using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Database;
using csharp_api.Model.Game;
using csharp_api.Model.Lobby;

namespace csharp_api.Services
{
    public class GameService
    {
        private IDatabase _database;

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
                } else if (detectiveCount > 0) {
                    // Assign to detective
                    role = "DETECTIVE";
                    detectiveCount--;
                }

                GamePlayer ply = new GamePlayer(players[playerIdx], role, code);
                gamePlayers.Add(ply);
            }

            GameMetadata gameInfo = await _database.GameCreate(lobbyInfo, gamePlayers);

            // Cache player information

            return gameInfo.GameId;
        }

        private static List<string> _generateAnalyzerCodes(int length)
        {
            var random = new Random();
            const string letters = "ABCDEFGHJKLMNOPQRSTUVWXYZ2345678";

            List<string> codes = new List<string>();

            while (codes.Count < length)
            {
               var code = new string(Enumerable.Repeat(letters, 4).Select(s => s[random.Next(s.Length)]).ToArray()); 

               if (codes.Contains(code)) {
                   continue;
               } else {
                   codes.Add(code);
               }
            }

            return codes;
        }
    }
}