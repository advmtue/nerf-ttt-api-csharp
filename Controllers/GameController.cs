using System.Net.Http;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Amazon.DynamoDBv2.Model;
using csharp_api.Transfer.Response.Error;
using csharp_api.Services;
using csharp_api.Database;

namespace csharp_api.Controllers
{
    public class ConfirmKillerRequest
    {
        public string killerId { get; set; }
    }

    [ApiController]
    [Route("game")]
    public class GameController : ControllerBase
    {
        private GameService _gameService;

        public GameController(GameService gameService)
        {
            _gameService = gameService;
        }

        [Authorize(Policy = "UserOnly")]
        [HttpPost]
        public async Task<IActionResult> Create()
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                return Ok(new { code = await _gameService.CreateGame(userId) });
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
        [HttpGet("{gameId}")]
        public async Task<IActionResult> Get([FromRoute] string gameId)
        {
            try
            {
                string userId = HttpContext.User.Identity.Name;
                return Ok(await _gameService.GetGameInfo(gameId, userId));
            }
            catch (GameNotFoundException)
            {
                return BadRequest(new APIError("No game found with matching ID", "ERR_GAME_NOT_FOUND"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{gameId}/start")]
        public async Task<IActionResult> Start([FromRoute] string gameId)
        {
            try
            {
                string userId = HttpContext.User.Identity.Name;
                await _gameService.StartGame(gameId, userId);

                return Ok();
            }
            catch (ConditionalCheckFailedException)
            {
                return BadRequest(new APIError("Either game cannot be started, or you are not the owner", "ERR_START_CONDITION_CHECK_FAIL"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"[GameController] Error: {e.Message}");
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/launch")]
        public async Task<IActionResult> Launch([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                await _gameService.LaunchGame(code, userId);

                return Ok();
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


        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("{code}")]
        public async Task<IActionResult> Close([FromRoute] string code)
        {
            try
            {
                await _gameService.AdminCloseGame(code);
                return Ok();
            }
            catch (LobbyNotFoundException)
            {
                return BadRequest(new APIError("Lobby not found", "ERR_LOBBY_NOT_FOUND"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/join")]
        public async Task<IActionResult> PlayerJoin([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                await _gameService.PlayerJoin(code, userId);
                return Ok(new { success = true });
            }
            catch (GameInProgressException)
            {
                return BadRequest(new APIError("Game is in progress.", "ERR_GAME_IN_PROGRESS"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{code}/leave")]
        public async Task<IActionResult> PlayerLeave([FromRoute] string code)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                await _gameService.PlayerLeave(code, userId);
                return Ok(new { success = true });
            }
            catch (GameInProgressException)
            {
                return BadRequest(new APIError("Game is in progress.", "ERR_GAME_IN_PROGRESS"));
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
                await _gameService.PlayerSetReady(code, userId);
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
                await _gameService.PlayerSetUnready(code, userId);
                return Ok(new { success = true });
            }
            catch (GameInProgressException)
            {
                return BadRequest(new APIError("Game is in progress.", "ERR_GAME_IN_PROGRESS"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpPost("{gameId}/confirmkiller")]
        public async Task<IActionResult> PlayerConfirmKiller([FromRoute] string gameId, [FromBody] ConfirmKillerRequest body)
        {
            try
            {
                string userId = HttpContext.User.Identity.Name;
                await _gameService.PlayerConfirmKiller(gameId, userId, body.killerId);

                return Ok();
            }
            catch (GameNotInProgressException)
            {
                return BadRequest(new APIError("Game is not in progress", "ERR_GAME_NOT_ACTIVE"));
            }
            catch (PlayerIsDeadException)
            {
                return BadRequest(new APIError("You are already dead", "ERR_PLY_ALREADY_DEAD"));
            }
            catch (PlayerIsAliveException)
            {
                return BadRequest(new APIError("You cannot confirm your killer if you have not died", "ERR_PLY_IS_ALIVE"));
            }
            catch (UserNotFoundException)
            {
                return BadRequest(new APIError("Specified killer is not in this game", "ERR_KILLER_NOT_FOUND"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }
    }
}