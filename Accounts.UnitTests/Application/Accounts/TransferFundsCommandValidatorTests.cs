using Accounts.Application.Accounts.Commands;
using FluentAssertions;

namespace Accounts.UnitTests.Application.Accounts
{
    public class TransferFundsCommandValidatorTests
    {
        [Fact]
        public void Fails_when_from_is_empty()
        {
            var v = new TransferFundsCommandValidator();
            var r = v.Validate(new TransferFundsCommand
            {
                FromAccountId = Guid.Empty,
                ToAccountId = Guid.NewGuid(),
                Amount = 10m
            });
            r.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Fails_when_to_equals_from()
        {
            var id = Guid.NewGuid();
            var v = new TransferFundsCommandValidator();
            var r = v.Validate(new TransferFundsCommand
            {
                FromAccountId = id,
                ToAccountId = id,
                Amount = 10m
            });
            r.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Fails_when_amount_not_positive()
        {
            var v = new TransferFundsCommandValidator();
            var r = v.Validate(new TransferFundsCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = 0m
            });
            r.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Passes_when_valid()
        {
            var v = new TransferFundsCommandValidator();
            var r = v.Validate(new TransferFundsCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = 25m
            });
            r.IsValid.Should().BeTrue();
        }
    }
}
