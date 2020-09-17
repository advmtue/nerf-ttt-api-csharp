using System;
using Amazon.DynamoDBv2;

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