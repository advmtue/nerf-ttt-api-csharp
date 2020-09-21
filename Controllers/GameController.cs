using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using csharp_api.Model.Game;
using csharp_api.Transfer.Response.Error;
using csharp_api.Services;

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
                GameMetadata gameData = await _gameService.Get(gameId);

                return Ok(gameData);
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
                GamePlayerInfo gameInfo = await _gameService.GetFilteredInfo(gameId, userId);

                return Ok(gameInfo);
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred", "ERR_UNKNOWN"));
            }
        }
    }
}