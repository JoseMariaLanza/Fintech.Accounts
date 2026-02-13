using Accounts.Domain.Accounts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accounts.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // modelBuilder.Entity<Account>().HasKey(x => x.Id);
            modelBuilder.Entity<Account>(b =>
            {
                b.ToTable("accounts");
                b.HasKey("Id");
                b.Property(x => x.OwnerName).IsRequired();
                b.Property(x => x.Balance).IsRequired();
            });

            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.ToTable("outbox_messages");
                b.HasKey("Id");
                b.Property(x => x.Type).HasMaxLength(200).IsRequired();
                b.Property(x => x.RoutingKey).HasMaxLength(200).IsRequired();
                b.Property(x => x.Payload).IsRequired();
                b.Property(x => x.CreatedAtUtc).IsRequired();
                b.Property(x => x.Status).IsRequired();
                b.Property(x => x.SentAtUtc);
                b.Property(x => x.RetryCount).IsRequired().HasDefaultValue(0);

                b.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("ix_created_at");
                b.HasIndex(x => x.Status).HasDatabaseName("ix_status");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
