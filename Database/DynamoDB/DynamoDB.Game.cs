using System.Threading;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Model.Game;
using Amazon.DynamoDBv2.Model;
using csharp_api.Model.User;

namespace csharp_api.Database.DynamoDB
{
    public partial class DynamoDBContext : IDatabase
    {
        public async Task<GameMetadata> GetGame(string gameId)
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

            return new GameMetadata(game.Item);
        }

        public async Task<List<GamePlayer>> GameGetPlayers(string gameId)
        {
            // TODO Paginate
            QueryResponse playerQuery = await _client.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "pk = :gameId AND begins_with(sk, :player)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":gameId", new AttributeValue($"GAME#{gameId}" ) },
                    { ":player", new AttributeValue($"PLAYER") }
                }
            });

            List<GamePlayer> players = new List<GamePlayer>();
            foreach (Dictionary<string, AttributeValue> item in playerQuery.Items)
            {
                players.Add(new GamePlayer(item));
            }

            return players;
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

            return new GamePlayer(playerResponse.Item);
        }

        public async Task<List<GameKill>> GameGetKills(string gameId)
        {
            QueryResponse killQuery = await _client.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "pk = :gameId AND begins_with(sk, :kill)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":gameId", new AttributeValue($"GAME#{gameId}") },
                    { ":kill", new AttributeValue("KILL#") }
                }
            });

            List<GameKill> kills = new List<GameKill>();

            killQuery.Items.ForEach(item =>
            {
                kills.Add(new GameKill(item));
            });

            return kills;
        }

        public async Task CreateGame(GameMetadata gameInfo)
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                Item = new System.Collections.Generic.Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"GAME#{gameInfo.Code}" } },
                    { "sk", new AttributeValue() { S = "metadata" } },
                    { "dateCreated", new AttributeValue() { N = gameInfo.DateCreated.ToString() } },
                    // Owner Id
                    { "GSI1-SK", new AttributeValue() { S = gameInfo.OwnerId } },
                    { "ownerName", new AttributeValue() { S = gameInfo.OwnerName } },
                    // Status
                    { "GSI1-PK", new AttributeValue() { S = gameInfo.Status } },
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
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}" )},
                    { "sk", new AttributeValue("metadata") }
                },
                ConditionExpression = "#ownerId = :callingPlayerId AND #status = :lobby",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#ownerId", "GSI1-SK" },
                    { "#status", "GSI1-PK" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":callingPlayerId", new AttributeValue(callingPlayerId) },
                    { ":lobby", new AttributeValue("LOBBY") },
                    { ":ingame", new AttributeValue("PREGAME") }
                },
                UpdateExpression = "SET #status = :ingame",
                TableName = _tableName
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
                        { ":role", new AttributeValue(player.Role) },
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
                UpdateExpression = "SET #status = :ingame",
                ConditionExpression = "#status = :pregame AND #ownerId = :callingPlayerId",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "GSI1-PK" },
                    { "#ownerId", "GSI1-SK" },
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pregame", new AttributeValue("PREGAME") },
                    { ":callingPlayerId", new AttributeValue(callingPlayerId) },
                    { ":ingame", new AttributeValue("INGAME") }
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
            // Update victim alive status
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}") },
                    { "sk", new AttributeValue($"PLAYER#{victim.UserId}") }
                },
                UpdateExpression = "SET #alive = :false",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#alive", "alive" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":false", new AttributeValue { BOOL = false } },
                    { ":true", new AttributeValue { BOOL = true } }
                },
                ConditionExpression = "#alive = :true"
            });

            // Calculate team kill
            bool wasTeamKill = false;
            if (victim.Role == killer.Role)
            {
                wasTeamKill = true;
            }
            else if (victim.Role == "INNOCENT" && killer.Role == "DETECTIVE")
            {
                wasTeamKill = true;
            }
            else if (victim.Role == "DETECTIVE" && killer.Role == "INNOCENT")
            {
                wasTeamKill = true;
            }

            await _client.PutItemAsync(new PutItemRequest
            {

                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}") },
                    { "sk", new AttributeValue($"KILL#{killer.UserId}#{victim.UserId}") },
                    { "killerName", new AttributeValue(killer.DisplayName) },
                    { "killerId", new AttributeValue(killer.UserId) },
                    { "victimName", new AttributeValue(victim.DisplayName) },
                    { "victimId", new AttributeValue(victim.UserId) },
                    { "wasTeamKill", new AttributeValue { BOOL = wasTeamKill } },
                    { "time", new AttributeValue { N = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() } },
                    // Analyzer information
                    { "GSI1-PK", new AttributeValue($"GAME#{gameId}") },
                    { "GSI1-SK", new AttributeValue(victim.AnalyzerCode) }
                }
            });
        }
    }
}