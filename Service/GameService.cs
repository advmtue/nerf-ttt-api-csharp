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
    public class GameInProgressException : Exception { }
    public class GameNotInProgressException : Exception { }
    public class PlayerIsDeadException : Exception { }

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

                return new GamePlayerBasic(player.UserId, player.DisplayName, player.Role);
            }).ToList();


            return new GamePlayerInfo
            {
                Role = localPlayer.Role,
                AnalyzerCode = localPlayer.AnalyzerCode,
                ScansRemaining = localPlayer.ScansRemaining,
                LastScanTime = localPlayer.LastScanTime,
                Players = gamePlayersBasic,
                Alive = localPlayer.IsAlive,
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
                await _GameEnd(gameId, winningTeam);
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
            // TODO Ensure game is in lobby phase

            await _database.AdminCloseGame(code);
            await _messageService.GameClose(code);

            this._usedGameCodes.Remove(code);
        }

        public async Task PlayerJoin(string gameCode, string userId)
        {
            // Check game is in lobby phase
            GameMetadata gameInfo = await _database.GetGame(gameCode);

            if (gameInfo.Status != "LOBBY")
            {
                throw new GameInProgressException();
            }

            Profile userProfile = await _database.GetUser(userId);
            await _database.GamePlayerJoin(gameCode, userProfile);
            await _messageService.PlayerJoin(gameCode, userProfile);
        }

        public async Task PlayerLeave(string gameCode, string userId)
        {
            // Check game is in lobby phase
            GameMetadata gameInfo = await _database.GetGame(gameCode);

            if (gameInfo.Status != "LOBBY")
            {
                throw new GameInProgressException();
            }

            await _database.GamePlayerLeave(gameCode, userId);
            await _messageService.PlayerLeave(gameCode, userId);
        }

        public async Task PlayerSetReady(string gameCode, string userId)
        {
            // Check game is in lobby phase
            GameMetadata gameInfo = await _database.GetGame(gameCode);

            if (gameInfo.Status != "LOBBY")
            {
                throw new GameInProgressException();
            }

            await _database.GamePlayerSetReady(gameCode, userId);
            await _messageService.PlayerSetReady(gameCode, userId);
        }

        public async Task PlayerSetUnready(string gameCode, string userId)
        {
            // Check game is in lobby phase
            GameMetadata gameInfo = await _database.GetGame(gameCode);

            if (gameInfo.Status != "LOBBY")
            {
                throw new GameInProgressException();
            }

            await _database.GamePlayerSetUnready(gameCode, userId);
            await _messageService.PlayerSetUnready(gameCode, userId);
        }

        public async Task PlayerConfirmKiller(string gameCode, string deadPlayerId, string killerId)
        {
            // Check that the game is in INGAME phase
            GameMetadata gameInfo = await _database.GetGame(gameCode);

            if (gameInfo.Status != "INGAME")
            {
                throw new GameNotInProgressException();
            }

            // Check that the calling player is not already dead
            // Can't use condition check because we still need player information for the kill log
            GamePlayer callingPlayer = await _database.GameGetPlayer(gameCode, deadPlayerId);

            if (!callingPlayer.IsAlive)
            {
                throw new PlayerIsDeadException();
            }

            // Check that the killerId is in the lobby
            // This should throw a UserNotFoundException if the player isn't in the lobby
            // Defaults to an unknown killer
            GamePlayer killer = new GamePlayer() { UserId = "UNKNOWN", DisplayName = "UNKNOWN", Role = "INNOCENT" };
            if (killerId != "UNKNOWN")
            {
                killer = await _database.GameGetPlayer(gameCode, killerId);
            }

            // set player dead and add kill log
            await _database.GamePlayerDie(gameCode, callingPlayer, killer);

            // check win conditions
            string winningTeam = await _CheckWinConditionAlive(gameCode);

            if (winningTeam == "NONE")
            {
                return;
            }
            else
            {
                await _GameEnd(gameCode, winningTeam);
            }
        }

        private async Task _GameEnd(string gameId, string winningTeam)
        {
            // Update game metadata
            await _database.EndGamePostPending(gameId, winningTeam);

            // Pull kill list
            List<GameKill> kills = await _database.GameGetKills(gameId);

            // Check unconfirmed kills
            List<GameKill> unconfirmedKills = new List<GameKill>();
            kills.ForEach(kill =>
            {
                if (kill.KillerId == "UNKNOWN")
                {
                    unconfirmedKills.Add(kill);
                }
            });

            if (unconfirmedKills.Count > 0)
            {
                // TODO Replace placeholder with something more meaningful
                List<GamePlayerBasic> playersToConfirm = unconfirmedKills.Select(kill => {
                    return new GamePlayerBasic(kill.VictimId, kill.VictimName, ":)" );
                }).ToList();

                await _messageService.SendConfirmKills(gameId, playersToConfirm);

                // Game will offically end once these players have confirmed their deaths;
                // Do nothing more at this point
            }
            else
            {
                // Game is officially ended
                await _database.EndGameComplete(gameId);
                await _messageService.GameEnd(gameId, winningTeam, kills);
            }
        }

        private async Task<string> _CheckWinConditionAlive(string gameCode)
        {
            List<GamePlayer> players = await _database.GameGetPlayers(gameCode);

            int innocentTeamAlive = 0; // Innocent + Detectives
            int traitorTeamAlive = 0; // Traitor

            foreach (GamePlayer player in players)
            {
                if (player.Role == "INNOCENT" || player.Role == "DETECTIVE")
                {
                    innocentTeamAlive++;
                }
                else
                {
                    traitorTeamAlive++;
                }
            }

            if (traitorTeamAlive == 0)
            {
                return "INNOCENT";
            }
            else if (innocentTeamAlive == 0)
            {
                return "TRAITOR";
            }
            else
            {
                return "NONE";
            }
        }
    }
}