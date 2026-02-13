using Accounts.Application.Common.Interfaces;
using Accounts.Domain.Accounts.Entities;
using Accounts.Infrastructure.Persistence;

namespace Accounts.Infrastructure.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AppDbContext _appDbContext;
        public AccountRepository(AppDbContext db) => _appDbContext = db;

        public Task<Account?> GetByIdAsync(Guid id) => _appDbContext.Accounts.FindAsync(id).AsTask();
        public Task AddAsync(Account account) => _appDbContext.Accounts.AddAsync(account).AsTask();
        public Task<int> SaveChangesAsync(CancellationToken ct) => _appDbContext.SaveChangesAsync(ct);
    }
}
