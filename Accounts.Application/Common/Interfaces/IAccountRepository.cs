using Accounts.Domain.Accounts.Entities;

namespace Accounts.Application.Common.Interfaces
{
    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(Guid id);
        Task AddAsync(Account account);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
