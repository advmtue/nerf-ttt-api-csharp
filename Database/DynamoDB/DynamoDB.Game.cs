using System.Linq;
using System.Threading;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Model.Game;
using Amazon.DynamoDBv2.Model;
using csharp_api.Model.User;
using csharp_api.Model.Player;
using csharp_api.Model.Roles;
using csharp_api.Model.Game.Kill;

namespace csharp_api.Database.DynamoDB
{
    public static class Mapper
    {
        public static GameInfo ToGameInfo(Dictionary<string, AttributeValue> item)
        {
            var info = new GameInfo
            {
                Code = item["pk"].S.Split("#")[1],
                OwnerId = item["GSI1-SK"].S,
                OwnerName = item["ownerName"].S,
                Status = StatusName.ToStatus(item["GSI1-PK"].S),
                DateCreated = item["dateCreated"].N,
            };

            // Possibly null
            if (item.ContainsKey("dateStarted"))
            {
                info.DateStarted = item["dateStarted"].N;
            }

            if (item.ContainsKey("dateLaunched"))
            {
                info.DateLaunched = item["dateLaunched"].N;
            }

            if (item.ContainsKey("dateEnded"))
            {
                info.DateEnded = item["dateEnded"].N;
            }

            if (item.ContainsKey("nextGameCode"))
            {
                info.NextGameCode = item["nextGameCode"].S;
            }

            if (item.ContainsKey("winningTeam"))
            {
                info.WinningTeam = item["winningTeam"].S;
            }

            return info;
        }

        public static GamePlayer ToGamePlayer(Dictionary<string, AttributeValue> item)
        {
            var player = new GamePlayer
            {
                UserId = item["userId"].S,
                DisplayName = item["displayName"].S,
                IsAlive = item["alive"].BOOL,
                Role = RoleInfo.ToRole(item["role"].S),
                AnalyzerCode = item["analyzerCode"].S,
                ScansRemaining = Int32.Parse(item["scansRemaining"].N),
                LastScanTime = item["lastScanTime"].N
            };

            if (item.ContainsKey("killerId"))
            {
                player.KillerId = item["killerId"].S;
            }

            if (item.ContainsKey("killTime"))
            {
                player.KillTime = item["killTime"].N;
            }

            return player;
        }

        public static LobbyPlayer ToLobbyPlayer(Dictionary<string, AttributeValue> item)
        {
            return new LobbyPlayer
            {
                UserId = item["userId"].S,
                DisplayName = item["displayName"].S,
                IsReady = item["ready"].BOOL
            };
        }
    }

    public partial class DynamoDBContext : IDatabase
    {
        public async Task<GameInfo> GetGameInfo(string gameId)
        {
            GetItemResponse game = await _client.GetItemAsync(new GetItemRequest
            {
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}") },
                    { "sk", new AttributeValue("metadata") }
                },
                TableName = _tableName
            });

            // Couldn't find game
            if (!game.IsItemSet)
            {
                throw new GameNotFoundException();
            }

            return Mapper.ToGameInfo(game.Item);
        }

        private async Task<QueryResponse> _GetGamePlayersItems(string gameCode)
        {
            // TODO Paginate
            return await _client.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "pk = :gameId AND begins_with(sk, :player)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":gameId", new AttributeValue($"GAME#{gameCode}" ) },
                    { ":player", new AttributeValue($"PLAYER") }
                }
            });
        }

        public async Task<List<GamePlayer>> GameGetPlayers(string gameCode)
        {
            QueryResponse playerQuery = await _GetGamePlayersItems(gameCode);

            return playerQuery.Items.Select(item => Mapper.ToGamePlayer(item)).ToList();
        }

        public async Task<List<LobbyPlayer>> GetLobbyPlayers(string gameCode)
        {
            QueryResponse playerQuery = await _GetGamePlayersItems(gameCode);

            return playerQuery.Items.Select(item => Mapper.ToLobbyPlayer(item)).ToList();
        }

        public async Task<GamePlayer> GameGetPlayer(string gameId, string userId)
        {
            GetItemResponse playerResponse = await _client.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}") },
                    { "sk", new AttributeValue($"PLAYER#{userId}") }
                }
            });

            if (!playerResponse.IsItemSet)
            {
                throw new UserNotFoundException();
            }

            return Mapper.ToGamePlayer(playerResponse.Item);
        }

        public async Task CreateGame(string gameCode, Profile ownerProfile)
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                Item = new System.Collections.Generic.Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"GAME#{gameCode}" } },
                    { "sk", new AttributeValue() { S = "metadata" } },
                    { "dateCreated", new AttributeValue() { N = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() } },
                    // Owner Id
                    { "GSI1-SK", new AttributeValue() { S = ownerProfile.UserId } },
                    { "ownerName", new AttributeValue() { S = ownerProfile.DisplayName } },
                    // Status
                    { "GSI1-PK", new AttributeValue() { S = StatusName.Lobby } },
                },
                ConditionExpression = "attribute_not_exists(pk)",
                TableName = _tableName
            });
        }

        public async Task LaunchGame(string gameId, string callingPlayerId, List<GamePlayer> players)
        {
            // Update game metadata
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}" )},
                    { "sk", new AttributeValue("metadata") }
                },
                UpdateExpression = "SET #status = :ingame, #dateLaunched = :dateNow",
                ConditionExpression = "#ownerId = :callingPlayerId AND #status = :lobby",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#ownerId", "GSI1-SK" },
                    { "#status", "GSI1-PK" },
                    { "#dateLaunched", "dateLaunched" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":callingPlayerId", new AttributeValue(callingPlayerId) },
                    { ":lobby", new AttributeValue("LOBBY") },
                    { ":ingame", new AttributeValue("PREGAME") },
                    { ":dateNow", new AttributeValue { N = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() } }
                },
            });

            // Update player information
            List<UpdateItemRequest> playerUpdates = new List<UpdateItemRequest>();
            foreach (GamePlayer player in players)
            {
                playerUpdates.Add(new UpdateItemRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "pk", new AttributeValue($"GAME#{gameId}") },
                        { "sk", new AttributeValue($"PLAYER#{player.UserId}") }
                    },
                    UpdateExpression = "SET #alive = :alive, #role = :role, #lastScanTime = :lastScanTime, #scansRemaining = :scansRemaining, #analyzerCode = :analyzerCode",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#alive", "alive" },
                        { "#role", "role" },
                        { "#lastScanTime", "lastScanTime" },
                        { "#scansRemaining", "scansRemaining" },
                        { "#analyzerCode", "analyzerCode" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":alive", new AttributeValue { BOOL = true } },
                        { ":role", new AttributeValue(RoleInfo.GetName(player.Role)) },
                        { ":lastScanTime", new AttributeValue { N = player.LastScanTime } },
                        { ":scansRemaining", new AttributeValue { N = player.ScansRemaining.ToString() } },
                        { ":analyzerCode", new AttributeValue(player.AnalyzerCode) }
                    },
                    TableName = _tableName
                });
            }

            // Perform player updates
            while (playerUpdates.Count > 0)
            {
                await _client.UpdateItemAsync(playerUpdates[0]);
                playerUpdates.RemoveAt(0);
            }
        }

        public async Task StartGame(string gameId, string callingPlayerId)
        {
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue($"GAME#{gameId}" ) },
                    { "sk", new AttributeValue("metadata") },
                },
                UpdateExpression = "SET #status = :ingame, #dateStarted = :dateNow",
                ConditionExpression = "#status = :pregame AND #ownerId = :callingPlayerId",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "GSI1-PK" },
                    { "#ownerId", "GSI1-SK" },
                    { "#dateStarted", "dateStarted" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pregame", new AttributeValue("PREGAME") },
                    { ":callingPlayerId", new AttributeValue(callingPlayerId) },
                    { ":ingame", new AttributeValue("INGAME") },
                    { ":dateNow", new AttributeValue { N = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() } }
                }
            });
        }

        public async Task EndGamePostPending(string gameId, string winningTeam)
        {
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}" ) },
                    { "sk", new AttributeValue("metadata") }
                },
                UpdateExpression = "SET #status = :postpending, #winningTeam = :winningTeam",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "GSI1-PK" },
                    { "#winningTeam", "winningTeam" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":postpending", new AttributeValue("POSTPENDING") },
                    { ":winningTeam", new AttributeValue(winningTeam) },
                    { ":ingame", new AttributeValue("INGAME") }
                },
                ConditionExpression = "#status = :ingame",
            });
        }

        public async Task EndGameComplete(string gameId)
        {
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}" )},
                    { "sk", new AttributeValue("metadata") }
                },
                UpdateExpression = "SET #status = :postgame",
                ConditionExpression = "#status = :postpending",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "GSI1-PK" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":postgame", new AttributeValue("POSTGAME") },
                    { ":postpending", new AttributeValue("POSTPENDING") }
                }
            });
        }

        public async Task AdminCloseGame(string lobbyCode)
        {
            // Delete the game
            await _client.DeleteItemAsync(new DeleteItemRequest()
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue($"GAME#{lobbyCode}") },
                    { "sk", new AttributeValue("metadata") }
                },
            });

            // FIXME Delete players
        }

        public async Task GamePlayerJoin(string gameCode, Profile userProfile)
        {
            await _client.PutItemAsync(new PutItemRequest()
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"GAME#{gameCode}" } },
                    { "sk", new AttributeValue() { S = $"PLAYER#{userProfile.UserId}" } },
                    { "displayName", new AttributeValue() { S = userProfile.DisplayName } },
                    { "userId", new AttributeValue() { S = userProfile.UserId } },
                    { "ready", new AttributeValue() { BOOL = false } }
                },
                ConditionExpression = "attribute_not_exists(sk)"
            });
        }

        public async Task GamePlayerLeave(string gameCode, string userId)
        {
            await _client.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"GAME#{gameCode}" } },
                    { "sk", new AttributeValue { S = $"PLAYER#{userId}" } },
                }
            });
        }

        public async Task GamePlayerSetReady(string lobbyCode, string userId)
        {
            // TODO Check lobby status

            UpdateItemRequest playerReadyRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"GAME#{lobbyCode}"}},
                    { "sk", new AttributeValue { S = $"PLAYER#{userId}"}}
                },
                UpdateExpression = "SET ready = :true",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":true", new AttributeValue { BOOL = true } }
                }
            };

            await _client.UpdateItemAsync(playerReadyRequest);
        }

        public async Task GamePlayerSetUnready(string lobbyCode, string userId)
        {
            // TODO Check lobby status

            UpdateItemRequest playerReadyRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"GAME#{lobbyCode}"}},
                    { "sk", new AttributeValue { S = $"PLAYER#{userId}"}}
                },
                UpdateExpression = "SET ready = :false",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":false", new AttributeValue { BOOL = false } }
                }
            };

            await _client.UpdateItemAsync(playerReadyRequest);
        }

        public async Task GamePlayerDie(string gameId, GamePlayer victim, GamePlayer killer)
        {
            // Calculate team kill
            bool wasTeamKill = false;
            if (victim.Role == killer.Role)
            {
                wasTeamKill = true;
            }
            else if (victim.Role == Role.INNOCENT && killer.Role == Role.DETECTIVE)
            {
                wasTeamKill = true;
            }
            else if (victim.Role == Role.DETECTIVE && killer.Role == Role.INNOCENT)
            {
                wasTeamKill = true;
            }

            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}") },
                    { "sk", new AttributeValue($"PLAYER#{victim.UserId}") }
                },
                UpdateExpression = "SET #alive = :false, #killerId = :killerId, #killTime = :killTime",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#alive", "alive" },
                    { "#killerId", "killerId" },
                    { "#killTime", "killTime" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":false", new AttributeValue { BOOL = false } },
                    { ":true", new AttributeValue { BOOL = true } },
                    { ":killerId", new AttributeValue(killer.UserId) },
                    { ":killTime", new AttributeValue { N = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() }}
                },
                ConditionExpression = "#alive = :true"
            });
        }

        public async Task GameConfirmKiller(string gameId, GamePlayer victim, GamePlayer killer)
        {
            bool wasTeamKill = false;
            if (victim.Role == killer.Role)
            {
                wasTeamKill = true;
            }
            else if (victim.Role == Role.INNOCENT && killer.Role == Role.DETECTIVE)
            {
                wasTeamKill = true;
            }
            else if (victim.Role == Role.DETECTIVE && killer.Role == Role.INNOCENT)
            {
                wasTeamKill = true;
            }

            // Update the kill log
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}") },
                    { "sk", new AttributeValue($"KILL#UNKNOWN#{victim.UserId}") },
                },
                UpdateExpression = "SET sk = :killSK, wasTeamKill = :wasTeamKill, killerName = :killerName, killerId = :killerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":killSK", new AttributeValue($"KILL#{killer.UserId}#{victim.UserId}") },
                    { ":wasTeamKill", new AttributeValue { BOOL = wasTeamKill } },
                    { ":killerId", new AttributeValue(killer.UserId) },
                    { ":killerName", new AttributeValue(killer.DisplayName) },
                }
            });
        }
    }
}