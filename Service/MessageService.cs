using System.Text;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using csharp_api.Model.User;

namespace csharp_api.Services.Message {
  public class MessageService {
    private IConfiguration _config;
    private string _socketURI;
    private HttpClient _client = new HttpClient();

    public MessageService(IConfiguration configuration) {
      _config = configuration.GetSection("SocketAPI");
      _socketURI = _config["URI"];
    }

    private async Task _MakeRequest(string URI, string body) {
      var content = new StringContent(body, Encoding.UTF8, "application/json");

      await _client.PostAsync(URI, content);
    }

    public async Task LobbyPlayerJoin(string lobbyCode, Profile player) {
      var data = JsonSerializer.Serialize<Profile>(player);
      var uri = $"{_socketURI}/lobby/{lobbyCode}/playerjoin";

      await _MakeRequest(uri, data);
    }

    public async Task LobbyPlayerLeave(string lobbyCode, string playerId) {
      var data = JsonSerializer.Serialize(new {playerId = playerId});
      var uri = $"{_socketURI}/lobby/{lobbyCode}/playerleave";

      await _MakeRequest(uri, data);
    }

    public async Task LobbyPlayerReady(string lobbyCode, string playerId) {
      var data = JsonSerializer.Serialize(new {playerId = playerId});
      var uri = $"{_socketURI}/lobby/{lobbyCode}/playerready";

      await _MakeRequest(uri, data);
    }

    public async Task LobbyPlayerUnready(string lobbyCode, string playerId) {
      var data = JsonSerializer.Serialize(new {playerId = playerId});
      var uri = $"{_socketURI}/lobby/{lobbyCode}/playerunready";

      await _MakeRequest(uri, data);
    }

    public async Task LobbyClose(string lobbyCode) {
      var uri = $"{_socketURI}/lobby/{lobbyCode}";

      await _client.DeleteAsync(uri);
    }
  }
}