using Accounts.Application.Accounts.DTOs;
using MediatR;

namespace Accounts.Application.Accounts.Queries.GetById
{
    public sealed record GetAccountByIdQuery(Guid id) : IRequest<AccountDto?>;
}
