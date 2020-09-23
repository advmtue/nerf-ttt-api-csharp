using System.Net.Http;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Amazon.DynamoDBv2.Model;
using csharp_api.Model.Game;
using csharp_api.Transfer.Response.Error;
using csharp_api.Services;
using csharp_api.Database;

namespace csharp_api.Controllers
{
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
        [HttpGet("{gameId}")]
        public async Task<IActionResult> Get([FromRoute] string gameId)
        {
            try
            {
                return Ok(await _gameService.Get(gameId));
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
        [HttpGet("{gameId}/info")]
        public async Task<IActionResult> GetInfo([FromRoute] string gameId)
        {
            try
            {
                string userId = HttpContext.User.Identity.Name;
                return Ok(await _gameService.GetFilteredInfo(gameId, userId));
            }
            catch (PlayerNotInGameException)
            {
                return BadRequest(new APIError("You are not a player in the requested game", "ERR_PLAYER_NOT_IN_GAME"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{gameId}/start")]
        public async Task<IActionResult> StartGame([FromRoute] string gameId)
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
    }
}