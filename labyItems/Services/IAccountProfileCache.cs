using labyItems.Models.Authentication;

namespace labyItems.Services;

public interface IAccountProfileCache
{
    Task<AccountProfile?> GetAsync(string email, string password);
    Task SaveAsync(string email, string password, AccountProfile profile);
}
