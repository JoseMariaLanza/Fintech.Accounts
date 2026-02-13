using Accounts.Application.Accounts.DTOs;
using Accounts.Application.Accounts.Queries.GetById;
using Accounts.Application.Common.Interfaces;
using Accounts.Domain.Accounts.Entities;
using FluentAssertions;
using MapsterMapper;
using Moq;

namespace Accounts.UnitTests.Application.Accounts
{
    public class GetAccountByIdHandlerTests
    {
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IMapper> _mapperMock;

        public GetAccountByIdHandlerTests()
        {
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _mapperMock = new Mock<IMapper>();
        }

        [Fact]
        public async Task Returns_null_when_account_not_found()
        {
            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(Guid.NewGuid()))
                .ReturnsAsync((Account?)null);

            var handler = new GetAccountByIdHandler(_accountRepositoryMock.Object, _mapperMock.Object);

            var dto = await handler.Handle(new GetAccountByIdQuery(Guid.NewGuid()), CancellationToken.None);

            dto.Should().BeNull();
        }

        [Fact]
        public async Task Returns_dto_when_account_exists()
        {
            var id = Guid.NewGuid();
            var entity = new Account(id, "Owner", 123m);
            _accountRepositoryMock
                .Setup(r => r.GetByIdAsync(id))
                .ReturnsAsync(entity);

            _mapperMock
                .Setup(m => m.Map<AccountDto>(entity))
                .Returns(new AccountDto
                {
                    Id        = id,
                    OwnerName = "Owner",
                    Balance   = 123m
                });

            var handler = new GetAccountByIdHandler(
                _accountRepositoryMock.Object,
                _mapperMock.Object);

            var dto = await handler.Handle(new GetAccountByIdQuery(id), CancellationToken.None);

            dto.Should().NotBeNull();
            dto.Id.Should().Be(id);
            dto.OwnerName.Should().Be("Owner");
            dto.Balance.Should().Be(123m);
        }
    }
}
