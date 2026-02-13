namespace Accounts.Domain.Accounts.Entities
{
    public class Account
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string OwnerName { get; set;} = string.Empty;
        public decimal Balance { get; set;}

        public Account() { }

        public Account(Guid id, string ownerName, decimal balance = 0m)
        {
            Id = id;
            OwnerName = ownerName;
            Balance = balance;
        }

        public void Credit(decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("The Amount to be credited should be positive.");
            Balance += amount;
        }

        public void Debit(decimal amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException("The Amount to be debited should be positive");
            if (amount > Balance) throw new InvalidOperationException("Insufficient funds to debit.");
            Balance -= amount;  
        }
    }
}
