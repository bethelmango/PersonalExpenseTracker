using System.Threading.Tasks;

namespace PersonalExpenseTracker.Services
{
    public interface IAuthService
    {
        Task<bool> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(string email, string password);
        Task LogoutAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<string> GetCurrentUserIdAsync();
    }
}