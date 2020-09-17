using csharp_api.Database;

namespace csharp_api.Services
{
    public class UserService
    {
        private IDatabase _database;

        public UserService(IDatabase database)
        {
            _database = database;
        }

        public void registerUser(string userId, string name)
        {
        }
    }
}