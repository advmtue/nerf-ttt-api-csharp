using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Database;
using csharp_api.Model.Game;
using csharp_api.Model.User;
using csharp_api.Services.Message;
using csharp_api.Model.Player;
using csharp_api.Model.Roles;
using csharp_api.Model.Game.Kill;
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
    public class PlayerIsAliveException : Exception { }

    public class GameService
    {
        private IDatabase _database;
        private MessageService _messageService;
        private List<string> _usedGameCodes = new List<string>();

        public GameService(IDatabase database, MessageService messageService)
        {
            _database = database;
            _messageService = messageService;
        }

        public List<GamePlayer> _InitializePlayers(List<LobbyPlayer> lobbyPlayers)
        {
            // Random for allocation of traitors
            var random = new Random();

            // TODO Determine the actual ratios for traitor and detective
            int traitorCount = (lobbyPlayers.Count / 6) + 1;
            int detectiveCount = (lobbyPlayers.Count / 8) + 1;
            int playerCount = lobbyPlayers.Count;

            List<string> analyzerList = _GenerateAnalyzerCodes(lobbyPlayers.Count);
            List<GamePlayer> gamePlayers = new List<GamePlayer>();

            while (analyzerList.Count != 0)
            {
                // Take a random player
                int playerIdx = random.Next(0, lobbyPlayers.Count);
                var ply = lobbyPlayers[playerIdx];

                // Analyzer code
                string analzyerCode = analyzerList[0];
                analyzerList.RemoveAt(0);

                Role role = Role.INNOCENT;
                int scansRemaining = 0;

                if (traitorCount > 0)
                {
                    // Assign to traitor
                    role = Role.TRAITOR;
                    traitorCount--;
                }
                else if (detectiveCount > 0)
                {
                    // Assign to detective
                    role = Role.DETECTIVE;
                    scansRemaining = 3;

                    detectiveCount--;
                }

                gamePlayers.Add(new GamePlayer(ply)
                {
                    LastScanTime = "0",
                    ScansRemaining = scansRemaining,
                    AnalyzerCode = analzyerCode,
                    Role = role,
                    IsAlive = true
                });

                // Remove the player from pool
                lobbyPlayers.RemoveAt(playerIdx);
            }

            return gamePlayers;
        }

        // Handler for aggregating game phase information into a single object
        public async Task<GameInfo> GetGameInfo(string gameId, string callingPlayerId)
        {
            GameInfo gameInfo = await _database.GetGameInfo(gameId);

            if (gameInfo.Status == Status.LOBBY)
            {
                return await _GetLobbyInfo(gameInfo);
            }
            else if (gameInfo.Status == Status.PREGAME)
            {
                GamePlayer callingPlayer = await _GetGamePlayer(gameInfo.Code, callingPlayerId);
                return await _GetPregameInfo(gameInfo, callingPlayer);
            }
            else if (gameInfo.Status == Status.INGAME)
            {
                GamePlayer callingPlayer = await _GetGamePlayer(gameInfo.Code, callingPlayerId);
                return await _GetIngameInfo(gameInfo, callingPlayer);
            }
            else if (gameInfo.Status == Status.POSTPENDING)
            {
                return await _GetPostPendingInfo(gameInfo);
            }
            else if (gameInfo.Status == Status.POSTGAME)
            {
                return await _GetPostGameInfo(gameInfo);
            }

            throw new GameNotFoundException();
        }

        private async Task<LobbyInfo> _GetLobbyInfo(GameInfo gameInfo)
        {
            return new LobbyInfo(gameInfo)
            {
                Players = await _GetLobbyPlayers(gameInfo.Code)
            };
        }

        private async Task<GamePlayer> _GetGamePlayer(string gameCode, string playerId)
        {
            return await _database.GameGetPlayer(gameCode, playerId);
        }

        private async Task<PregameInfo> _GetPregameInfo(GameInfo gameInfo, GamePlayer callingPlayer)
        {
            return new PregameInfo(gameInfo)
            {
                Players = await _GetFilteredGamePlayers(gameInfo, callingPlayer),
                LocalPlayer = callingPlayer
            };
        }

        private async Task<IngameInfo> _GetIngameInfo(GameInfo gameInfo, GamePlayer callingPlayer)
        {
            return new IngameInfo(gameInfo)
            {
                Players = await _GetFilteredGamePlayers(gameInfo, callingPlayer),
                LocalPlayer = callingPlayer
            };
        }

        private async Task<PostPendingInfo> _GetPostPendingInfo(GameInfo gameInfo)
        {
            return new PostPendingInfo(gameInfo)
            {
                WaitingFor = await _GetKillConfirmationWaiting(gameInfo),
                Players = (await _GetGamePlayers(gameInfo.Code))
                            .Select(p => new GamePlayerBasic(p.UserId, p.DisplayName, p.Role))
                            .ToList()
            };
        }

        private async Task<PostGameInfo> _GetPostGameInfo(GameInfo gameInfo)
        {
            return new PostGameInfo(gameInfo)
            {
                GameKills = await _GetGameKills(gameInfo.Code)
            };
        }

        private async Task<List<GamePlayerBasic>> _GetKillConfirmationWaiting(GameInfo gameInfo)
        {
            List<GameKill> gameKills = await _GetGameKills(gameInfo.Code);

            return gameKills
                    .FindAll(k => k.KillerId == "UNKNOWN")
                    .Select(k => new GamePlayerBasic(k.VictimId, k.VictimName, Role.INNOCENT)).ToList();
        }

        private async Task<List<GameKill>> _GetGameKills(string gameCode)
        {
            // Get game players
            List<GamePlayer> players = await _GetGamePlayers(gameCode);

            return players
                .FindAll(player => player.KillerId != null)
                .Select(victim =>
                {
                    // Killer can be null here, handle UNKNOWN
                    GamePlayer killer = players.Find(pl => pl.UserId == victim.KillerId);

                    if (killer == null)
                    {
                        return new GameKill
                        {
                            KillerId = "UNKNOWN",
                            KillerName = "UNKNOWN",
                            KillerRole = Role.INNOCENT,
                            VictimId = victim.UserId,
                            VictimName = victim.DisplayName,
                            VictimRole = victim.Role,
                            Time = victim.KillTime
                        };
                    }
                    else
                    {
                        return new GameKill
                        {
                            KillerId = killer.UserId,
                            KillerName = killer.DisplayName,
                            KillerRole = killer.Role,
                            VictimId = victim.UserId,
                            VictimName = victim.DisplayName,
                            VictimRole = victim.Role,
                            Time = victim.KillTime
                        };
                    }

                }).ToList();

        }

        private async Task<List<GamePlayerBasic>> _GetFilteredGamePlayers(GameInfo gameInfo, GamePlayer callingPlayer)
        {
            List<GamePlayer> players = await _GetGamePlayers(gameInfo.Code);

            return players.Select(pl =>
            {
                return new GamePlayerBasic(pl)
                {
                    Role = RoleInfo.CanSee(callingPlayer.Role, pl.Role) ? pl.Role : Role.INNOCENT
                };
            }).ToList().FindAll(pl => pl.UserId != callingPlayer.UserId);
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

        private async Task<List<GamePlayer>> _GetGamePlayers(string gameId)
        {
            // TODO Caching game player information
            return await _database.GameGetPlayers(gameId);
        }

        private async Task EndGameTimer(string gameId)
        {
            // TODO Remove magic strings throughout this function

            var playerList = await _GetGamePlayers(gameId);

            // Calculate winning team
            int numDetectiveAlive = 0;

            foreach (GamePlayer player in playerList)
            {
                if (player.RoleName == "DETECTIVE" && player.IsAlive)
                {
                    numDetectiveAlive++;
                }
            }

            string winningTeam = (numDetectiveAlive > 0) ? "INNOCENT" : "TRAITOR";

            try
            {
                await _GameEnd(gameId, winningTeam);
            }
            catch (ConditionalCheckFailedException)
            {
                Console.WriteLine("[GameService] Game timer ended but game is not INGAME --IGNORING--");
            }
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

        private string _GenerateCode(int length)
        {
            Console.WriteLine("[GameService] Generating new lobby code");

            var random = new Random();
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            string code = "default";
            bool available = false;
            int attempts = 0;
            do
            {
                code = new string(Enumerable.Repeat(letters, length).Select(s => s[random.Next(s.Length)]).ToArray());

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

            Console.WriteLine($"[GameService] Code = {code}, Attempts = {attempts}");
            _usedGameCodes.Add(code);

            return code;
        }

        public async Task<string> CreateGame(string ownerId)
        {
            // TODO Lookup if owner has any active games

            // Pull owner profile
            Profile ownerProfile = await _database.GetUser(ownerId);

            // Generate a game code
            string gameCode = _GenerateCode(5);

            // Create the game
            await _database.CreateGame(gameCode, ownerProfile);

            return gameCode;
        }

        private async Task<List<LobbyPlayer>> _GetLobbyPlayers(string gameCode)
        {
            return await _database.GetLobbyPlayers(gameCode);
        }

        public async Task LaunchGame(string code, string callingUserId)
        {
            GameInfo gameInfo = await _database.GetGameInfo(code);

            // Check the owner of the lobby is the calling player
            if (gameInfo.OwnerId != callingUserId)
            {
                throw new PlayerNotOwnerException();
            }

            // Check the lobby status is LOBBY
            if (gameInfo.Status != Status.LOBBY)
            {
                throw new LobbyNotStartableException();
            }

            var lobbyPlayers = await _GetLobbyPlayers(code);

            // Check that there is at least 3 players
            // FIXME Remove debug 0
            if (lobbyPlayers.Count < 0)
            {
                throw new MinimumPlayersException();
            }

            // Check that all players are ready
            var potentialUnreadyPlayer = lobbyPlayers.Find(p => !p.IsReady);
            if (potentialUnreadyPlayer != null)
            {
                throw new PlayersNotReadyException();
            }

            // Allocate roles
            List<GamePlayer> gamePlayers = _InitializePlayers(lobbyPlayers);

            // Save information
            await _database.LaunchGame(code, callingUserId, gamePlayers);

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
            GameInfo gameInfo = await _database.GetGameInfo(gameCode);

            if (gameInfo.Status != Status.LOBBY)
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
            GameInfo gameInfo = await _database.GetGameInfo(gameCode);

            if (gameInfo.Status != Status.LOBBY)
            {
                throw new GameInProgressException();
            }

            await _database.GamePlayerLeave(gameCode, userId);
            await _messageService.PlayerLeave(gameCode, userId);
        }

        public async Task PlayerSetReady(string gameCode, string userId)
        {
            // Check game is in lobby phase
            GameInfo gameInfo = await _database.GetGameInfo(gameCode);

            if (gameInfo.Status != Status.LOBBY)
            {
                throw new GameInProgressException();
            }

            await _database.GamePlayerSetReady(gameCode, userId);
            await _messageService.PlayerSetReady(gameCode, userId);
        }

        public async Task PlayerSetUnready(string gameCode, string userId)
        {
            // Check game is in lobby phase
            GameInfo gameInfo = await _database.GetGameInfo(gameCode);

            if (gameInfo.Status != Status.LOBBY)
            {
                throw new GameInProgressException();
            }

            await _database.GamePlayerSetUnready(gameCode, userId);
            await _messageService.PlayerSetUnready(gameCode, userId);
        }

        public async Task PlayerConfirmKiller(string gameCode, string deadPlayerId, string killerId)
        {
            // TODO Player cannot kill themselves (Anti-jordan mechanism)

            GamePlayer callingPlayer = await _database.GameGetPlayer(gameCode, deadPlayerId);
            GameInfo gameInfo = await _database.GetGameInfo(gameCode);

            if (gameInfo.Status == Status.INGAME)
            {
                // INGAME Phase

                // Check that the calling player is not already dead
                // Can't use condition check because we still need player information for the kill log
                if (!callingPlayer.IsAlive)
                {
                    throw new PlayerIsDeadException();
                }

                // Pull killer information or use UNKNOWN killer
                GamePlayer killer = new GamePlayer() { UserId = "UNKNOWN", DisplayName = "UNKNOWN", Role = Role.INNOCENT };
                if (killerId != "UNKNOWN")
                {
                    killer = await _database.GameGetPlayer(gameCode, killerId);
                }

                // Offically kill the player
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
            else if (gameInfo.Status == Status.POSTPENDING)
            {
                // Cannot use unknown killer in postpending confirmation page
                if (killerId == "UNKNOWN")
                {
                    throw new UserNotFoundException();
                }

                // Alive players cannot confirm their death in postpending
                if (callingPlayer.IsAlive)
                {
                    throw new PlayerIsAliveException();
                }

                GamePlayer killer = await _GetGamePlayer(gameCode, killerId);
                await _database.GameConfirmKiller(gameCode, callingPlayer, killer);
                await _messageService.PlayerConfirmKill(gameCode, callingPlayer.UserId);

                if (await _CheckAllDeathsConfirmed(gameCode))
                {
                    // Enter POSTGAME
                    await _database.EndGameComplete(gameCode);

                    // Pull winning team and kills
                    await _messageService.GameEnd(gameCode, gameInfo.WinningTeam, await _GetGameKills(gameCode));
                }
            }
            else
            {
                throw new GameNotInProgressException();
            }
        }

        private async Task<bool> _CheckAllDeathsConfirmed(string gameCode)
        {
            // Pull kill list
            var gameKills = await _GetGameKills(gameCode);

            // Look for any kills where the killerID is unknown
            GameKill possibleUnknownKill = gameKills.Find(kill => kill.KillerId == "UNKNOWN");
            return possibleUnknownKill == null;
        }

        private async Task _GameEnd(string gameId, string winningTeam)
        {
            // Update game metadata
            await _database.EndGamePostPending(gameId, winningTeam);

            // Pull kill list
            List<GameKill> kills = await _GetGameKills(gameId);

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
                List<GamePlayerBasic> playersToConfirm = unconfirmedKills.Select(kill =>
                {
                    return new GamePlayerBasic(kill.VictimId, kill.VictimName, Role.INNOCENT);
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
                if (!player.IsAlive) continue;

                if (player.Role == Role.INNOCENT || player.Role == Role.DETECTIVE)
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

        public async Task<List<GamePlayerBasic>> GetWaitingKillConfirmation(string gameId)
        {
            var gameKills = await _GetGameKills(gameId);

            List<GamePlayerBasic> awaitingPlayers = new List<GamePlayerBasic>();

            gameKills.ForEach(kill =>
            {
                if (kill.KillerId == "UNKNOWN")
                {
                    awaitingPlayers.Add(new GamePlayerBasic(kill.VictimId, kill.VictimName, Role.INNOCENT));
                }
            });

            return awaitingPlayers;
        }
    }
}