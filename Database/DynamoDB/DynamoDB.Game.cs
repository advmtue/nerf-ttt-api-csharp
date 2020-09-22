using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using csharp_api.Model.Game;
using csharp_api.Model.Lobby;
using Amazon.DynamoDBv2.Model;

namespace csharp_api.Database.DynamoDB
{
    public partial class DynamoDBContext : IDatabase
    {
        public async Task<GameMetadata> GameCreate(LobbyMetadata lobbyInfo, List<GamePlayer> players)
        {
            // Create game metadata
            GameMetadata newGame = new GameMetadata
            {
                GameId = Guid.NewGuid().ToString(),
                Name = lobbyInfo.Name,
                DateLaunched = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(),
                Status = "PREGAME",
                OwnerId = lobbyInfo.OwnerId,
                OwnerName = lobbyInfo.OwnerName,
                LobbyCode = lobbyInfo.Code
            };

            // Build transaction 
            BatchWriteItemRequest batchWriteItemRequest = new BatchWriteItemRequest();
            List<WriteRequest> writeRequests = new List<WriteRequest>();

            // Add game metadata
            writeRequests.Add(new WriteRequest
            {
                PutRequest = new PutRequest
                {
                    Item = new Dictionary<string, AttributeValue> {
                        { "pk", new AttributeValue($"GAME#{newGame.GameId}") },
                        { "sk", new AttributeValue("metadata") },
                        { "name", new AttributeValue(newGame.Name) },
                        { "dateLaunched", new AttributeValue { N = newGame.DateLaunched }},
                        { "status", new AttributeValue("PREGAME") },
                        { "ownerName", new AttributeValue(newGame.OwnerName) },
                        { "ownerId", new AttributeValue(newGame.OwnerId) },
                        { "lobbyCode", new AttributeValue(newGame.LobbyCode) }
                    },
                }
            });

            // Add player list
            foreach (GamePlayer player in players)
            {
                writeRequests.Add(new WriteRequest
                {
                    PutRequest = new PutRequest
                    {
                        Item = new Dictionary<string, AttributeValue> {
                            { "pk", new AttributeValue($"GAME#{newGame.GameId}") },
                            { "sk", new AttributeValue($"PLAYER#{player.UserId}") },
                            { "displayName", new AttributeValue(player.DisplayName) },
                            { "role", new AttributeValue(player.Role) },
                            { "isAlive", new AttributeValue { BOOL = player.IsAlive }},
                            { "analyzerCode", new AttributeValue(player.AnalyzerCode) },
                            { "scansRemaining", new AttributeValue { N = player.ScansRemaining.ToString() }},
                            { "lastScanTime", new AttributeValue(player.LastScanTime)}
                        },
                    }
                });
            }

            // Update lobby status
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                Key = new Dictionary<string, AttributeValue> {
                        { "pk", new AttributeValue($"LOBBY#{lobbyInfo.Code}") },
                        { "sk", new AttributeValue("metadata") }
                    },
                UpdateExpression = "SET #lobbyStatus = :ingame, #currentGameId = :gameId",
                ExpressionAttributeNames = new Dictionary<string, string> {
                        { "#lobbyStatus", "GSI1-PK" },
                        { "#currentGameId", "currentGameId" }
                    },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                        { ":ingame", new AttributeValue("INGAME") },
                        { ":gameId", new AttributeValue(newGame.GameId) }
                    },
                TableName = _tableName
            }
            );

            // FIXME Needs review -- Can be implemented as a DoWhile (as per the wiki)
            // https://docs.aws.amazon.com/sdkfornet1/latest/apidocs/html/M_Amazon_DynamoDB_AmazonDynamoDBClient_BatchWriteItem.htm
            await _client.BatchWriteItemAsync(new Dictionary<string, List<WriteRequest>> {
                { _tableName, writeRequests }
            });

            return newGame;
        }

        public async Task<GameMetadata> GetGameById(string gameId)
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

        public async Task<List<GamePlayer>> GetGamePlayers(string gameId)
        {
            QueryResponse playerQuery = await _client.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "pk = :gameId AND begins_with(sk, :player)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":gameId", new AttributeValue($"GAME#{gameId}") },
                    { ":player", new AttributeValue("PLAYER#") }
                }
            });

            List<GamePlayer> gamePlayers = new List<GamePlayer>();

            foreach (Dictionary<string, AttributeValue> item in playerQuery.Items)
            {
                gamePlayers.Add(new GamePlayer(item));
            }

            return gamePlayers;
        }

        public async Task GameStart(string gameId, string callingPlayerId)
        {
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue($"GAME#{gameId}" ) },
                    { "sk", new AttributeValue("metadata") },
                },
                ConditionExpression = "#status = :pregame AND #ownerId = :callingPlayerId",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "status" },
                    { "#ownerId", "ownerId" },
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pregame", new AttributeValue("PREGAME") },
                    { ":callingPlayerId", new AttributeValue(callingPlayerId) }
                }
            });
        }
    }
}