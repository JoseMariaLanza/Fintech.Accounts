using FluentValidation;

namespace Accounts.Application.Accounts.Commands
{
    public class TransferFundsCommandValidator : AbstractValidator<TransferFundsCommand>
    {
        public TransferFundsCommandValidator()
        {
            RuleFor(x => x.FromAccountId).NotEmpty();
            RuleFor(x => x.ToAccountId)
                .NotEmpty()
                .NotEqual(x => x.FromAccountId).WithMessage("The destination account should be different than the origin.");
            RuleFor(x => x.Amount).GreaterThan(0);
        }
    }
}
