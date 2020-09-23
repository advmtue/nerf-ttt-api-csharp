using System.Text;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using csharp_api.Model.User;

namespace csharp_api.Services.Message
{
    public class MessageService
    {
        private IConfiguration _config;
        private string _socketURI;
        private HttpClient _client = new HttpClient();

        public MessageService(IConfiguration configuration)
        {
            _config = configuration.GetSection("SocketAPI");
            _socketURI = _config["URI"];
        }

        private async Task _MakeRequest(string URI, string body)
        {
            try
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                await _client.PostAsync(URI, content);
            }
            catch (HttpRequestException e)
            {
                // TODO Retry the request
                Console.WriteLine($"[MessageService] Failed to notify subscribers of action :{e.Message}");
            }
        }

        public async Task PlayerJoin(string gameCode, Profile player)
        {
            var data = JsonSerializer.Serialize<Profile>(player);
            var uri = $"{_socketURI}/game/{gameCode}/playerjoin";

            await _MakeRequest(uri, data);
        }

        public async Task PlayerLeave(string gameCode, string playerId)
        {
            var data = JsonSerializer.Serialize(new { playerId = playerId });
            var uri = $"{_socketURI}/game/{gameCode}/playerleave";

            await _MakeRequest(uri, data);
        }

        public async Task PlayerSetReady(string gameCode, string playerId)
        {
            var data = JsonSerializer.Serialize(new { playerId = playerId });
            var uri = $"{_socketURI}/game/{gameCode}/playerready";

            await _MakeRequest(uri, data);
        }

        public async Task PlayerSetUnready(string gameCode, string playerId)
        {
            var data = JsonSerializer.Serialize(new { playerId = playerId });
            var uri = $"{_socketURI}/game/{gameCode}/playerunready";

            await _MakeRequest(uri, data);
        }

        public async Task GameClose(string gameCode)
        {
            var uri = $"{_socketURI}/game/{gameCode}";

            await _client.DeleteAsync(uri);
        }

        public async Task GameLaunch(string gameId)
        {
            var data = JsonSerializer.Serialize(new { gameId = gameId });
            var uri = $"{_socketURI}/game/{gameId}/launched";

            await _MakeRequest(uri, data);
        }

        public async Task GameStart(string gameId)
        {
            var data = JsonSerializer.Serialize(new { gameId = gameId });
            var uri = $"{_socketURI}/game/{gameId}/started";

            await _MakeRequest(uri, data);
        }

        public async Task GameEndTimer(string gameId)
        {
            var data = JsonSerializer.Serialize(new { gameId = gameId });
            var uri = $"{_socketURI}/game/{gameId}/ended";

            await _MakeRequest(uri, data);
        }
    }
}