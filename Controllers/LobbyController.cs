using System.Collections.Generic;
using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Amazon.DynamoDBv2.Model;

using csharp_api.Model.Lobby;
using csharp_api.Services;
using csharp_api.Database;
using csharp_api.Transfer.Response.Error;

namespace csharp_api.Controllers
{
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
        public async Task<IActionResult> CreateNewLobby()
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                return Ok(await _lobbyService.Create(userId));
            }
            catch (UserNotFoundException)
            {
                return BadRequest(new APIError("Could not lookup your userId", "ERR_USERID_INVALID"));
            }
            catch (CodePoolExhaustedException)
            {
                return BadRequest(new APIError("Lobby code pool is exhausted", "ERR_CODE_POOL_EXHAUSTED"));
            }
            catch (ConditionalCheckFailedException)
            {
                // TODO, retry
                return BadRequest(new APIError("An unexpected lobby code collision occurred", "ERR_CODE_COLLISION"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}")]
        public async Task<IActionResult> GetByCode([FromRoute] string code)
        {
            try
            {
                return Ok(await _lobbyService.GetByCode(code));
            }
            catch (LobbyNotFoundException)
            {
                return BadRequest(new APIError("Lobby not found", "ERR_LOBBY_NOT_FOUND"));
            }
        }


        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("{code}")]
        public async Task<IActionResult> CloseLobby([FromRoute] string code)
        {
            try
            {
                await _lobbyService.CloseLobbyByAdmin(code);
                return Ok();
            }
            catch (LobbyNotFoundException)
            {
                return BadRequest(new APIError("Lobby not found", "ERR_LOBBY_NOT_FOUND"));
            }
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

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/ready")]
        public async Task<IActionResult> PlayerSetReady([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                await _lobbyService.PlayerSetReady(code, userId);
                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/unready")]
        public async Task<IActionResult> PlayerSetUnready([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                await _lobbyService.PlayerSetUnready(code, userId);
                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/start")]
        public async Task<IActionResult> Start([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                var gameCode = await _lobbyService.Start(code, userId);

                return Ok(new { gameId = gameCode });
            }
            catch (PlayerNotOwnerException)
            {
                return BadRequest(new APIError("You are not the lobby owner", "ERR_NOT_OWNER"));
            }
            catch (LobbyNotStartableException)
            {
                return BadRequest(new APIError("Lobby is not in a startable state", "ERR_LOBBY_NOT_STARTABLE"));
            }
            catch (MinimumPlayersException)
            {
                return BadRequest(new APIError("Minimum player count not met", "ERR_MINIMUM_PLAYERS"));
            }
            catch (PlayersNotReadyException)
            {
                return BadRequest(new APIError("Some players are not ready", "ERR_PLAYERS_NOT_READY"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"[LobbyController] Exception :{e.Message}");
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }
    }
}