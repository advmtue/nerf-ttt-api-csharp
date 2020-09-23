using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System;
using System.Text;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;

using csharp_api.Database;
using csharp_api.Model.User;

namespace csharp_api.Services
{
    public class TokenManager
    {
        private IDatabase _database;
        private JwtHeader _jwtHeader;
        private JwtSecurityTokenHandler _jwtHandler = new JwtSecurityTokenHandler();
        private TokenValidationParameters _validationParameters;
        private IConfiguration _config;

        public TokenManager(IDatabase database, IConfiguration config)
        {
            _database = database;
            _config = config.GetSection("TokenManagement");

            var TokenSigningKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(_config["SigningKey"]));
            var TokenSigningCredentials = new SigningCredentials(TokenSigningKey, "HS256");

            // Cache signing headers for consistency
            _jwtHeader = new JwtHeader(TokenSigningCredentials);

            _validationParameters = new TokenValidationParameters()
            {
                ValidateLifetime = true,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidIssuer = _config["Issuer"],
                ValidAudience = _config["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = TokenSigningKey
            };
        }

        private JwtPayload _CreatePayload(List<Claim> claims, DateTime expiry)
        {
            return new JwtPayload(
                _config["Issuer"],
                _config["Audience"],
                claims,
                DateTime.Now,
                expiry
            );
        }

        public JwtSecurityToken ValidateToken(string token)
        {
            SecurityToken outToken;
            _jwtHandler.ValidateToken(token, _validationParameters, out outToken);

            return (JwtSecurityToken)outToken;
        }

        public string CreateRefreshToken(Profile profile)
        {
            // Add userId and a unique token ID to claims
            List<Claim> claims = new List<Claim> {
                new Claim(JwtRegisteredClaimNames.Sub, profile.UserId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Create a refresh token which is valid for a month
            JwtPayload payload = _CreatePayload(claims, DateTime.Now.AddMonths(1));

            // Return the encoded token
            return _jwtHandler.WriteToken(new JwtSecurityToken(_jwtHeader, payload));
        }

        public async Task<string> CreateAccessToken(string refreshToken)
        {
            // Validate the token
            JwtSecurityToken fullRefreshToken = ValidateToken(refreshToken);

            // TODO Check token blacklist

            // Pull user from database
            Profile profile = await _database.GetUserById(fullRefreshToken.Subject);

            // Encode userId, accessLevel into token
            List<Claim> claims = new List<Claim> {
                new Claim("userId", profile.UserId),
                new Claim("accessLevel", profile.AccessLevel),
            };

            // Create an access token which is valid for 10 minutes
            // TODO Reduce token expiry time, currently set to a debug time for 2 hours
            JwtPayload payload = _CreatePayload(claims, DateTime.Now.AddHours(2));

            return _jwtHandler.WriteToken(new JwtSecurityToken(_jwtHeader, payload));
        }
    }
}