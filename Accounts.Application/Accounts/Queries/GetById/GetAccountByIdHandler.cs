using Accounts.Application.Accounts.DTOs;
using Accounts.Application.Common.Interfaces;
using MapsterMapper;
using MediatR;

namespace Accounts.Application.Accounts.Queries.GetById
{
    public sealed class GetAccountByIdHandler : IRequestHandler<GetAccountByIdQuery, AccountDto?>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;

        public GetAccountByIdHandler(IAccountRepository accountRepository, IMapper mapper)
        {
            _accountRepository = accountRepository;
            _mapper = mapper;
        }

        public async Task<AccountDto?> Handle(GetAccountByIdQuery request, CancellationToken ct)
        {
            var account = await _accountRepository.GetByIdAsync(request.id);
            return account is null ? null : _mapper.Map<AccountDto>(account);
        }
    }
}
