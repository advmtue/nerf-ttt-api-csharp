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
    public partial class DynamoDBContext : IDatabase
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
    }
}