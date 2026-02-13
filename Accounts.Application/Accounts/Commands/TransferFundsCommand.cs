using MediatR;

namespace Accounts.Application.Accounts.Commands
{
    public class TransferFundsCommand : IRequest<Unit>
    {
        public Guid FromAccountId { get; set; }
        public Guid ToAccountId { get; set; }
        public decimal Amount { get; set; }
    }
}
