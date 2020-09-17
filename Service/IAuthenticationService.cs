using System;
using System.Threading.Tasks;

namespace csharp_api.Services.Discord
{
    public class AuthProviderErrorException : Exception
    {
        public AuthProviderErrorException() { }
    }

    public class UserLookupErrorException : Exception
    {
        public UserLookupErrorException() { }
    }

    public interface IAuthenticationService<AuthenticationType>
    {
        Task<AuthenticationType> Authenticate(string code);
    }
}