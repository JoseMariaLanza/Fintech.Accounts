namespace Accounts.API.Requests
{
    public record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount);
}
