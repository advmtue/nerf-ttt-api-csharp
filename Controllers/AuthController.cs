using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

using csharp_api.Database;
using csharp_api.Services;
using csharp_api.Services.Discord;
using csharp_api.Model.User;
using csharp_api.Model.User.Discord;
using csharp_api.Transfer.Response.Discord;

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
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [AllowAnonymous]
        public async Task<IActionResult> GetDiscordAuth([FromQuery(Name = "code")] string code)
        {
            string token;
            try
            {
                Profile profile = await _discord.Authenticate(code);
                token = _tokenManager.CreateRefreshToken(profile);
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

            // Return refresh token
            return Ok(new { refreshToken = token });
        }

        [HttpPost("token")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAccessToken([FromBody] AccessTokenRequest request)
        {
            try
            {
                string accessToken = await _tokenManager.CreateAccessToken(request.refreshToken);
                return Ok(new { accessToken = accessToken });
            }
            catch (Exception)
            {
                // TODO Token validation and database exception differences
                return BadRequest(new APIError("An unknown error occurred.", "ERR_UNKNOWN"));
            }

        }
    }
}
