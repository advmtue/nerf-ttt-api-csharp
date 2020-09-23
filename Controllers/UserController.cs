using System.Security.Claims;
using System;
using Microsoft.AspNetCore.Mvc;
using csharp_api.Model.User;
using System.Threading.Tasks;
using csharp_api.Database;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using csharp_api.Services;
using csharp_api.Transfer.Response.Error;

namespace csharp_api.Controllers
{
    public class RegistrationRequest
    {
        public string name { get; set; }
    }

    [ApiController]
    [Route("user")]
    public class UserController : ControllerBase
    {
        private IDatabase _database;
        private UserService _userService;

        public UserController(IDatabase database, UserService userService)
        {
            _database = database;
            _userService = userService;
        }

        [Authorize]
        [HttpGet("@me")]
        public async Task<IActionResult> getSelfProfile()
        {
            ClaimsIdentity user = HttpContext.User.Identity as ClaimsIdentity;
            string userId = user.Name;

            Profile profile;
            try
            {
                profile = await _database.GetUser(userId);
            }
            catch (Exception)
            {
                return BadRequest(new APIError("An unknown error occurred.", "ERR_UNKNOWN"));
            }

            if (profile == null)
            {
                return BadRequest(new APIError("User not found", "ERR_UNKNOWN"));
            }

            return Ok(profile);
        }

        [Authorize(Policy = "RegistrationOnly")]
        [HttpPost("@register")]
        public async Task<IActionResult> registerUser([FromBody] RegistrationRequest registrationRequest)
        {
            string userId = HttpContext.User.Identity.Name;

            try
            {
                // TODO Move this to the user service
                await _database.RegisterUser(userId, registrationRequest.name);
            }
            catch (Exception)
            {
                // TODO Better error handling
                Console.WriteLine("[UserController] Failed to create user");
                return BadRequest(new APIError("Failed to register user.", "ERR_UNKNOWN"));
            }

            return Ok(new { success = true });
        }
    }
}