using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

using csharp_api.Database;
using csharp_api.Services;
using csharp_api.Services.Discord;
using csharp_api.Model.User;

using csharp_api.Transfer.Response.Error;
using csharp_api.Transfer.Request.Token;

namespace csharp_api.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private DiscordAuthenticator _discord;
        private TokenManager _tokenManager;
        private IDatabase _database;

        public AuthController(DiscordAuthenticator discordAuth, TokenManager tokenManager, IDatabase database)
        {
            _database = database;
            _discord = discordAuth;
            _tokenManager = tokenManager;
        }

        [HttpGet("discord")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDiscordAuth([FromQuery(Name = "code")] string code)
        {
            try
            {
                Profile profile = await _discord.Authenticate(code);
                return Ok(new { refreshToken = _tokenManager.CreateRefreshToken(profile)});
            }
            catch (AuthProviderErrorException)
            {
                return BadRequest(new APIError("Authentication Failure", "ERR_AUTH_FAILED"));
            }
            catch (UserLookupErrorException)
            {
                return BadRequest(new APIError("Database query for Discord authentication failed", "ERR_DB_LOOKUP"));
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred.", "ERR_UNKNOWN"));
            }
        }

        [HttpPost("token")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAccessToken([FromBody] AccessTokenRequest request)
        {
            try
            {
                return Ok(new { accessToken = await _tokenManager.CreateAccessToken(request.refreshToken) });
            }
            catch (Exception)
            {
                // FIXME Token validation and database exception differences
                return BadRequest(new APIError("An unknown error occurred.", "ERR_UNKNOWN"));
            }
        }
    }
}
