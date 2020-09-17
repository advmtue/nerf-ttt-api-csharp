using System.Linq.Expressions;
using System.Reflection.PortableExecutable;
using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using csharp_api.Transfer.Response.Discord;
using csharp_api.Model.User;
using csharp_api.Database;
using csharp_api.Model.User.Discord;
using System.Threading.Tasks;

using csharp_api.Model.Lobby;
using csharp_api.Controllers;

namespace csharp_api.Database.DynamoDB
{

    public class DynamoDBContext : IDatabase
    {
        private readonly AmazonDynamoDBClient _client;
        private readonly string _tableName = "ttt_testing";

        public DynamoDBContext()
        {
            try
            {
                _client = new AmazonDynamoDBClient();
                Console.WriteLine("[Database] Connected to DynamoDB successfully");
            }
            catch (Exception)
            {
                Console.WriteLine("[Database] Failed to connect to DynamoDB");
            }
        }

        // Todo split into partial
        public async Task<Profile> GetUserByDiscord(DiscordUser discordUser)
        {
            // Create a GetItemRequest for User
            GetItemRequest getDiscordLoginRequest = DiscordLogin.BuildGetRequest(discordUser.id);
            getDiscordLoginRequest.TableName = this._tableName;

            // Perform response
            GetItemResponse discordLoginResponse;
            try
            {
                discordLoginResponse = await this._client.GetItemAsync(getDiscordLoginRequest);
            }
            catch (Exception ex)
            {
                // TODO Handle database exceptions
                Console.WriteLine("[Database] Failed to pull discord user");
                throw ex;
            }

            // User doesn't exist
            if (!discordLoginResponse.IsItemSet)
            {
                return null;
            }

            // Extract userId
            string userId = discordLoginResponse.Item["userId"].S;

            // Return the user
            return await GetUserById(userId);
        }

        public async Task<Profile> CreateUserByDiscord(DiscordUser discordUser)
        {
            // Goal: Create a registration-level user with a 5 minute TTL

            string userId = Guid.NewGuid().ToString();

            // Create a new discord login
            DiscordLogin newLogin = new DiscordLogin()
            {
                UserId = userId.ToString(),
                DiscordId = discordUser.id,
            };

            // Create a sparse profile
            Profile newProfile = new Profile()
            {
                UserId = userId,
                AccessLevel = "registration",
                DisplayName = "Unregistered User",
            };

            PutItemRequest discordPutRequest = newLogin.BuildPutRequest();
            PutItemRequest profilePutRequest = newProfile.BuildPutRequest();

            // Assign table name
            discordPutRequest.TableName = _tableName;
            profilePutRequest.TableName = _tableName;

            try
            {
                await _client.PutItemAsync(discordPutRequest);
                await _client.PutItemAsync(profilePutRequest);
            }
            catch (Exception)
            {
                Console.WriteLine("[Database] Failed to insert discord login or profile.");
                throw new DefaultDatabaseException();
            }

            return newProfile;
        }

        public async Task<Profile> GetUserById(string userId)
        {
            GetItemRequest profileRequest = Profile.BuildGetRequest(userId);
            profileRequest.TableName = _tableName;

            GetItemResponse profileResponse;
            try
            {
                profileResponse = await _client.GetItemAsync(profileRequest);
            }
            catch (Exception)
            {
                Console.WriteLine("[Database] Failed to lookup user by ID");
                throw new DefaultDatabaseException();
            }

            if (!profileResponse.IsItemSet)
            {
                return null;
            }
            else
            {
                return Profile.CreateFromItem(profileResponse.Item);
            }
        }

        public async Task RegisterUser(string userId, string name)
        {
            UpdateItemRequest nameUpdateRequest = new UpdateItemRequest()
            {
                TableName = _tableName,
                Key = new System.Collections.Generic.Dictionary<string, AttributeValue> {
                    { "pk", new AttributeValue() { S = $"USER#{userId}"} },
                    { "sk", new AttributeValue() { S = "profile" } }
                },
                UpdateExpression = "SET #displayName = :newDisplayName, accessLevel = :userAccessLevel",
                ConditionExpression = "accessLevel = :registrationAccessLevel",
                ExpressionAttributeNames = new System.Collections.Generic.Dictionary<string, string> {
                    { "#displayName", "GSI1-SK" }
                },
                ExpressionAttributeValues = new System.Collections.Generic.Dictionary<string, AttributeValue> {
                    { ":newDisplayName", new AttributeValue() { S = name } },
                    { ":userAccessLevel", new AttributeValue() { S = "user" } },
                    { ":registrationAccessLevel", new AttributeValue() { S = "registration" } }
                },
            };

            try
            {
                await _client.UpdateItemAsync(nameUpdateRequest);
            }
            catch (Exception)
            {
                Console.WriteLine("[Database] Failed to update user registration status");
                throw new DefaultDatabaseException();
            }
        }

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

        public async Task CloseLobbyByAdmin(string lobbyCode)
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

        public async Task<List<LobbyPlayer>> GetLobbyPlayers(string lobbyCode)
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

        public async Task PlayerJoinLobby(string lobbyCode, Profile userProfile)
        {
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

        public async Task PlayerLeaveLobby(string lobbyCode, string userId)
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
    }
}