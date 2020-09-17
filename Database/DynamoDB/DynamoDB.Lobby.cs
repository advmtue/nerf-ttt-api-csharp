using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using csharp_api.Model.Lobby;
using csharp_api.Model.User;

namespace csharp_api.Database.DynamoDB
{
    public partial class DynamoDBContext : IDatabase
    {
        public async Task CreateLobby(Metadata lobbyInfo)
        {
            PutItemRequest createLobbyRequest = new PutItemRequest()
            {
                Item = new System.Collections.Generic.Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"LOBBY#{lobbyInfo.code}" } },
                    { "sk", new AttributeValue() { S = "metadata" } },
                    { "name", new AttributeValue() { S = lobbyInfo.name } },
                    { "dateCreated", new AttributeValue() { N = lobbyInfo.dateCreated.ToString() } },
                    { "roundCount", new AttributeValue() { N = lobbyInfo.roundCount.ToString() } },
                    { "playerCount", new AttributeValue() { N = lobbyInfo.playerCount.ToString() } },
                    // Owner Id
                    { "GSI1-SK", new AttributeValue() { S = lobbyInfo.ownerId } },
                    { "ownerName", new AttributeValue() { S = lobbyInfo.ownerName } },
                    // Status
                    { "GSI1-PK", new AttributeValue() { S = lobbyInfo.status } }
                },
                ConditionExpression = "attribute_not_exists(pk)",
                TableName = _tableName
            };

            await _client.PutItemAsync(createLobbyRequest);
        }

        public async Task<Metadata> GetLobbyByCode(string lobbyCode)
        {
            GetItemRequest getLobbyRequest = new GetItemRequest()
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>() {
                    { "pk", new AttributeValue() { S = $"LOBBY#{lobbyCode}"} },
                    { "sk", new AttributeValue() { S = "metadata"} }
                }
            };

            GetItemResponse item = await _client.GetItemAsync(getLobbyRequest);

            if (!item.IsItemSet)
            {
                throw new LobbyNotFoundException();
            }

            return new Metadata(item.Item);
        }

        public async Task LobbyCloseByAdmin(string lobbyCode)
        {
            // Delete that bad boy
            DeleteItemRequest deleteLobbyRequest = new DeleteItemRequest()
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"LOBBY#{lobbyCode}" } },
                    { "sk", new AttributeValue() { S = "metadata" } },
                },
            };

            await _client.DeleteItemAsync(deleteLobbyRequest);
        }

        public async Task<List<LobbyPlayer>> LobbyGetPlayers(string lobbyCode)
        {
            QueryRequest playerQuery = new QueryRequest()
            {
                TableName = _tableName,
                KeyConditionExpression = "pk = :lobbyId AND begins_with(sk, :player)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":lobbyId", new AttributeValue() { S = $"LOBBY#{lobbyCode}" } },
                    { ":player", new AttributeValue() { S = "PLAYER#" } },
                }
            };

            QueryResponse playerResponse = await _client.QueryAsync(playerQuery);

            List<LobbyPlayer> players = new List<LobbyPlayer>();

            playerResponse.Items.ForEach(item =>
            {
                players.Add(new LobbyPlayer(item));
            });

            return players;
        }

        public async Task LobbyPlayerJoin(string lobbyCode, Profile userProfile)
        {
            // Todo check game status
            PutItemRequest addPlayerRequest = new PutItemRequest()
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"LOBBY#{lobbyCode}" } },
                    { "sk", new AttributeValue() { S = $"PLAYER#{userProfile.UserId}" } },
                    { "displayName", new AttributeValue() { S = userProfile.DisplayName } },
                    { "userId", new AttributeValue() { S = userProfile.UserId } },
                    { "ready", new AttributeValue() { BOOL = false } }
                },
                ConditionExpression = "attribute_not_exists(sk)"
            };

            UpdateItemRequest increasePlayerCountRequest = new UpdateItemRequest()
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                     { "pk", new AttributeValue() { S = $"LOBBY#{lobbyCode}" } },
                     { "sk", new AttributeValue() { S = "metadata" } }
                 },
                ExpressionAttributeNames = new Dictionary<string, string> {
                     { "#playerCount", "playerCount"}
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":incAmount", new AttributeValue() { N = "1" } }
                },
                UpdateExpression = "SET #playerCount = #playerCount + :incAmount"
            };

            await _client.PutItemAsync(addPlayerRequest);
            await _client.UpdateItemAsync(increasePlayerCountRequest);
        }

        public async Task LobbyPlayerLeave(string lobbyCode, string userId)
        {
            // TODO Check game status

            DeleteItemRequest removePlayerRequest = new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"LOBBY#{lobbyCode}" } },
                    { "sk", new AttributeValue { S = $"PLAYER#{userId}" } },
                }
            };

            UpdateItemRequest decreasePlayerCountRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"LOBBY#{lobbyCode}" } },
                    { "sk", new AttributeValue { S = "metadata" } },
                },
                UpdateExpression = "SET #playerCount = #playerCount - :decAmount",
                ExpressionAttributeNames = new Dictionary<string, string> {
                    { "#playerCount", "playerCount" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":decAmount", new AttributeValue { N = "1" } }
                }
            };

            await _client.DeleteItemAsync(removePlayerRequest);
            await _client.UpdateItemAsync(decreasePlayerCountRequest);
        }

        public async Task LobbyPlayerSetReady(string lobbyCode, string userId)
        {
            // TODO Check lobby status

            UpdateItemRequest playerReadyRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"LOBBY#{lobbyCode}"}},
                    { "sk", new AttributeValue { S = $"PLAYER#{userId}"}}
                },
                UpdateExpression = "SET ready = :true",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":true", new AttributeValue { BOOL = true } }
                }
            };

            await _client.UpdateItemAsync(playerReadyRequest);
        }

        public async Task LobbyPlayerSetUnready(string lobbyCode, string userId)
        {
            // TODO Check lobby status

            UpdateItemRequest playerReadyRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue { S = $"LOBBY#{lobbyCode}"}},
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