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
                    { ":ingame", new AttributeValue("INGAME") }
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
                    { "#ownerId", "ownerId" },
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pregame", new AttributeValue("PREGAME") },
                    { ":callingPlayerId", new AttributeValue(callingPlayerId) },
                    { ":ingame", new AttributeValue("INGAME") }
                }
            });
        }

        public async Task EndGameByTime(string gameId, string winningTeam)
        {
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue($"GAME#{gameId}" ) },
                    { "sk", new AttributeValue("metadata") }
                },
                UpdateExpression = "SET #status = :postgame, #winningTeam = :winningTeam",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "status" },
                    { "#winningTeam", "winningTeam" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":postgame", new AttributeValue("POSTGAME") },
                    { ":winningTeam", new AttributeValue(winningTeam) },
                    { ":ingame", new AttributeValue("INGAME") }
                },
                ConditionExpression = "#status = :ingame",
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

        public async Task GamePlayerJoin(string lobbyCode, Profile userProfile)
        {
            // TODO check game status

            await _client.PutItemAsync(new PutItemRequest()
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"GAME#{lobbyCode}" } },
                    { "sk", new AttributeValue() { S = $"PLAYER#{userProfile.UserId}" } },
                    { "displayName", new AttributeValue() { S = userProfile.DisplayName } },
                    { "userId", new AttributeValue() { S = userProfile.UserId } },
                    { "ready", new AttributeValue() { BOOL = false } }
                },
                ConditionExpression = "attribute_not_exists(sk)"
            });
        }

        public async Task GamePlayerLeave(string lobbyCode, string userId)
        {
            // TODO Check game status

            await _client.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"GAME#{lobbyCode}" } },
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
    }
}