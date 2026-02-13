using Accounts.API.Middlewares;
using Accounts.Application.Accounts.Commands;
using Accounts.Application.Common.Behaviors;
using Accounts.Application.Common.Interfaces;
using Accounts.Application.Common.Mappings;
using Accounts.Infrastructure.Messaging;
using Accounts.Infrastructure.Outbox;
using Accounts.Infrastructure.Persistence;
using Accounts.Infrastructure.Persistence.Seeding;
using Accounts.Infrastructure.Repositories;
using Fintech.Shared.Messaging;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/accounts-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// EF Core + Npgsql + Optional Seeder
// 1) Tomamos el flag desde appsettings.Development.json (default true si no existe)
bool seedingEnabled = builder.Configuration.GetValue<bool>("Seeding:Enabled", true);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Fintech"));
#if DEBUG
    if (builder.Environment.IsDevelopment() && seedingEnabled)
    {
        // Ejecuta el seeding en Dev cuando EF corre Migrate/EnsureCreated/Update-Database
        options
            .UseSeeding((ctx, _) =>
             {
                 // tooling de EF puede requerir sync: invocamos el método async de forma síncrona
                 AccountsDevSeed.RunAsync((AppDbContext)ctx, CancellationToken.None).GetAwaiter().GetResult();
             })
            .UseAsyncSeeding(async (ctx, _, ct) =>
            {
                await AccountsDevSeed.RunAsync((AppDbContext)ctx, ct);
            });
    }
#endif
});

// Messaging
//builder.Services.AddSingleton<IEventBus, InMemoryEventBus>(); // Proceso local
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>(); // RabbitMQ

// Accounts repository
builder.Services.AddScoped<IAccountRepository, AccountRepository>();

// OutboxMessage repository
builder.Services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

// Dispatcher de outbox (nuevo)
builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();

// Mapster (scan perfiles en Application)
var cfg = TypeAdapterConfig.GlobalSettings;
cfg.Scan(Assembly.GetAssembly(typeof(MappingRegister))!);
builder.Services.AddSingleton(cfg);
// builder.Services.AddScoped<IMapper, ServiceMapper>(); ServiceMapper no está en la versión 7.4.0
// Solución que no depende de ServiceMapper
builder.Services.AddScoped<IMapper>(sp =>
    new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));

// FluentValidation.DependencyInjectionExtensions + MediatR
builder.Services.AddValidatorsFromAssemblyContaining<TransferFundsCommandValidator>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<TransferFundsCommand>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// MVC + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//// Solo Dev/Test
//#if DEBUG
//// builder.Services.AddDbContext<NotificationsDb>(opt => opt.UseInMemoryDatabase("notifis"));
//#endif
//builder.Services.AddDbContext<NotificationsDb>(
//    opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDb")));

//builder.Services.AddScoped<INotificationsDb>(sp => sp.GetRequiredService<NotificationsDb>());

var app = builder.Build();
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();
app.Run();

public partial class Program { }
