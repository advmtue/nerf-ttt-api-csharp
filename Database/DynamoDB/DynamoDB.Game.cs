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
            TransactWriteItemsRequest batchWriteItemRequest = new TransactWriteItemsRequest();
            batchWriteItemRequest.TransactItems = new List<TransactWriteItem>();

            // Add game metadata
            batchWriteItemRequest.TransactItems.Add(new TransactWriteItem
            {
                Put = new Put
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
                    TableName = _tableName,
                }
            });

            // Add player list
            foreach (GamePlayer player in players)
            {
                batchWriteItemRequest.TransactItems.Add(new TransactWriteItem
                {
                    Put = new Put
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
                        TableName = _tableName,
                    }
                });
            }

            // Update lobby status
            batchWriteItemRequest.TransactItems.Add(new TransactWriteItem
            {
                Update = new Update {
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
            });

            // FIXME This won't be able to transact more than ~20 players at a time. Find a workaround (or pagination)
            await _client.TransactWriteItemsAsync(batchWriteItemRequest);

            return newGame;
        }
    }
}