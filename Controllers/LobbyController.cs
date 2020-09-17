using System.Collections.Generic;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using csharp_api.Model.Lobby;
using csharp_api.Services;
using csharp_api.Database;
using csharp_api.Model;
using csharp_api.Transfer.Response.Error;

namespace csharp_api.Controllers
{
    public class NewLobbyRequest
    {
        public string name { get; set; }
    }

    [ApiController]
    [Route("lobby")]
    public class LobbyController : ControllerBase
    {
        private IDatabase _database;
        private LobbyService _lobbyService;

        public LobbyController(IDatabase database, LobbyService lobbyService)
        {
            _database = database;
            _lobbyService = lobbyService;
        }

        [Authorize(Policy = "UserOnly")]
        [HttpPost]
        public async Task<IActionResult> CreateNewLobby([FromBody] NewLobbyRequest lobbyInfo)
        {
            string userId = HttpContext.User.Identity.Name;

            Metadata lobbyMeta = await _lobbyService.Create(lobbyInfo, userId);

            return Ok(lobbyMeta);
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}")]
        public async Task<IActionResult> GetByCode([FromRoute] string code)
        {
            Metadata lobbyMeta;
            try
            {
                lobbyMeta = await _lobbyService.GetByCode(code);
            }
            catch (LobbyNotFoundException)
            {
                return BadRequest(new APIError("Lobby not found", "ERR_LOBBY_NOT_FOUND"));
            }

            return Ok(lobbyMeta);
        }


        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("{code}")]
        public async Task<IActionResult> CloseLobby([FromRoute] string code)
        {
            try
            {
                await _lobbyService.CloseLobbyByAdmin(code);
            }
            catch (LobbyNotFoundException)
            {
                return BadRequest(new APIError("Lobby not found", "ERR_LOBBY_NOT_FOUND"));
            }

            return Ok(new { success = true });
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/players")]
        public async Task<IActionResult> GetLobbyPlayers([FromRoute] string code)
        {
            try
            {
                List<LobbyPlayer> lobbyPlayers = await _lobbyService.GetLobbyPlayers(code);
                return Ok(lobbyPlayers);
            }
            catch (LobbyNotFoundException)
            {
                return BadRequest(new APIError("Lobby not found", "ERR_LOBBY_NOT_FOUND"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/join")]
        public async Task<IActionResult> PlayerJoinLobby([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                await _lobbyService.PlayerJoinLobby(code, userId);
                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/leave")]
        public async Task<IActionResult> PlayerLeaveLobby([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                await _lobbyService.PlayerLeaveLobby(code, userId);
                return Ok(new { success = true });
            }
            catch (Exception)
            {
                // TODO Error handling
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNONW"));
            }
        }
    }
}