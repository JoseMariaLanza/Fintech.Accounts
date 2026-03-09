# Fintech.Accounts

Accounts microservice managing account balances and fund transfers. Publishes integration events via RabbitMQ using the transactional outbox pattern.

## Architecture

Clean Architecture with CQRS via MediatR. Dependency flows inward:

```
API -> Application -> Domain
API -> Infrastructure -> Application, Domain
```

```
Accounts.Domain/                 # Entities only, zero external deps
├── Account.cs                    # Credit()/Debit() with invariant guards
└── OutboxMessage.cs              # Factory Create<T>(), state machine (MarkAsSent/MarkAsFailed)

Accounts.Application/            # CQRS handlers, DTOs, ports, validation
├── Accounts/
│   ├── Commands/TransferFunds/   # TransferFundsCommand + handler
│   ├── Queries/GetAccountById/   # GetAccountByIdQuery + handler
│   ├── DTOs/                     # AccountDto, TransferFundsDto
│   └── Mappings/                 # Mapster IRegister
├── Common/
│   ├── Interfaces/               # IAccountRepository, IOutboxMessageRepository
│   └── Behaviors/                # ValidationBehavior (FluentValidation pipeline)

Accounts.Infrastructure/         # EF Core, repositories, RabbitMQ
├── Persistence/
│   ├── AppDbContext.cs            # EF Core context (PostgreSQL)
│   ├── Repositories/             # Account + OutboxMessage repos
│   └── Migrations/               # EF Core migrations
├── Messaging/
│   ├── RabbitMqEventBus.cs        # IEventBus implementation
│   └── OutboxDispatcher.cs        # Publishes pending outbox messages

Accounts.API/                    # ASP.NET Core host
├── AccountsController.cs         # Single controller, throws freely
├── ExceptionHandlingMiddleware.cs # Maps exceptions to HTTP status codes
└── Program.cs                    # DI composition

Fintech.Accounts.McpServer/      # MCP tools for AI access to transfer data

Accounts.UnitTests/              # xUnit + Moq + FluentAssertions
Accounts.IntegrationTests/       # Testcontainers (PostgreSQL, RabbitMQ) + WebApplicationFactory
```

## Technology stack

| Component | Version |
|---|---|
| .NET | 8.0 |
| EF Core (PostgreSQL) | 9.0 |
| MediatR | 13 |
| FluentValidation | 12 |
| Mapster | latest |
| RabbitMQ.Client | 7.2 |
| Serilog | latest |
| Fintech.Shared (NuGet) | 1.0.3 |

## Commands

```bash
# Build
dotnet build Fintech.Accounts.sln

# Test all
dotnet test Fintech.Accounts.sln

# Unit tests only
dotnet test Accounts.UnitTests/Accounts.UnitTests.csproj

# Integration tests (requires Docker)
dotnet test Accounts.IntegrationTests/Accounts.IntegrationTests.csproj

# Run
dotnet run --project Accounts.API

# Migrations
dotnet ef migrations add <Name> --project Accounts.Infrastructure --startup-project Accounts.API
dotnet ef database update --project Accounts.Infrastructure --startup-project Accounts.API
```

Swagger UI: `http://localhost:5235/swagger` (Development only)

## API

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/accounts/{id}` | Get account by ID |
| POST | `/api/accounts/transfer` | Transfer funds between accounts |

## Key patterns

- **Transactional outbox**: account changes + outbox message saved atomically in one `SaveChangesAsync`
- **Idempotent dispatch**: `OutboxDispatcher` marks messages as Sent/Failed after publishing
- **Validation pipeline**: `ValidationBehavior` runs FluentValidation before MediatR handlers
- **Exception middleware**: `ValidationException` -> 400, `KeyNotFoundException` -> 404, unhandled -> 500

## Integration

```
Fintech.Accounts ──publishes──> TransferCompleted (RabbitMQ)
                                 └── exchange: fintech.events
                                 └── routing key: transfer.completed
                                 └── consumed by: Fintech.Notifications

Fintech.Accounts ──uses──> Fintech.Shared (NuGet 1.0.3)
                            └── TransferCompleted event contract
                            └── IEventBus interface
```

## Dev seeding

In DEBUG+Development: Alice (id `aaa...`) with balance 1000, Bob (id `bbb...`) with balance 500.
