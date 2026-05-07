using System.Threading.Tasks;

namespace PersonalExpenseTracker.Services
{

    /// Dummy auth implementation used for development. Always treats the
    /// user as logged in with a fixed user id.
    public class DummyAuthService : IAuthService
    {
        private const string DemoUserId = "demo-user";

        public Task<bool> LoginAsync(string email, string password)
        {
            // Pretend login always succeeds
            return Task.FromResult(true);
        }

        public Task<bool> RegisterAsync(string email, string password)
        {
            // Pretend registration always succeeds
            return Task.FromResult(true);
        }

        public Task LogoutAsync()
        {
            // No-op for dummy auth
            return Task.CompletedTask;
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            // Always "logged in" in this dummy implementation
            return Task.FromResult(true);
        }

        public Task<string?> GetCurrentUserIdAsync()
        {
            // Always return the same demo user id
            return Task.FromResult<string?>(DemoUserId);
        }
    }
}