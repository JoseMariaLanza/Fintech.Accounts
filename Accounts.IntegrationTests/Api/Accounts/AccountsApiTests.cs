using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace Accounts.IntegrationTests.Api.Accounts
{
    public class AccountsApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _applicationFactory;
        public AccountsApiTests(WebApplicationFactory<Program> applicationFactory) => _applicationFactory = applicationFactory;

        [Fact]
        public async Task GetAccount_returns_404_when_not_found()
        {
            var client = _applicationFactory.CreateClient();
            var resp = await client.GetAsync($"/api/accounts/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Transfer_returns_400_when_validation_fails()
        {
            var client = _applicationFactory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/accounts/transfer", new
            {
                fromAccountId = Guid.NewGuid(),
                toAccountId = Guid.NewGuid(),
                amount = -10
            });
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
