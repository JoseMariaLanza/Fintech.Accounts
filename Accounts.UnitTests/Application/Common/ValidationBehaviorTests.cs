using Accounts.Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace Accounts.UnitTests.Application.Common
{
    public sealed class DummyRequest : IRequest<Unit>
    {
        public int Value { get; set; }
    }

    public sealed class DummyValidator : AbstractValidator<DummyRequest>
    {
        public DummyValidator()
        {
            RuleFor(x => x.Value).GreaterThan(0);
        }
    }

    public class ValidationBehaviorTests
    {
        [Fact]
        public async Task Throws_ValidationException_when_request_is_invalid()
        {
            var validator = new IValidator<DummyRequest>[] { new DummyValidator() };
            var behavior = new ValidationBehavior<DummyRequest, Unit>(validator);

            RequestHandlerDelegate<Unit> Next = (CancellationToken _) => Task.FromResult(Unit.Value);

            var act = () => behavior.Handle(new DummyRequest { Value = 0 }, Next, CancellationToken.None);

            await act.Should().ThrowAsync<ValidationException>();
        }

        [Fact]
        public async Task Passes_to_next_when_valid()
        {
            var validators = new IValidator<DummyRequest>[] { new DummyValidator() };
            var behavior = new ValidationBehavior<DummyRequest, Unit>(validators);

            RequestHandlerDelegate<Unit> Next = (CancellationToken _) => Task.FromResult(Unit.Value);

            var result = await behavior.Handle(
                new DummyRequest { Value = 1 },
                Next,
                CancellationToken.None);

            result.Should().Be(Unit.Value);
        }

        [Fact]
        public async Task Passes_when_no_validators_registered()
        {
            var validators = Array.Empty<IValidator<DummyRequest>>();
            var behaviors = new ValidationBehavior<DummyRequest, Unit>(validators);

            RequestHandlerDelegate<Unit> Next = (CancellationToken _) => Task.FromResult(Unit.Value);

            var result = await behaviors.Handle(
                new DummyRequest { Value = 1 },
                Next,
                CancellationToken.None);

            result.Should().Be(Unit.Value);
        }
    }
}
