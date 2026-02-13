//using System.Threading.Tasks;
//using MCPSharp;

//namespace Fintech.Accounts.McpServer;

//internal class Program
//{
//    public static async Task Main(string[] args)
//    {
//        // Registra explícitamente la clase con tools
//        MCPServer.Register<TransferTools>();

//        // Inicia un servidor MCP stdio (lectura/escritura por stdin/stdout)
//        await MCPServer.StartAsync("FintechAccountsTools", "1.0.0");
//    }
//}

using Accounts.Infrastructure.Persistence;
using MCPSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Threading.Tasks;

namespace Fintech.Accounts.McpServer;

internal class Program
{
    public static async Task Main(string[] args)
    {
        // 1) Cargar configuración desde appsettings.Development.json (linkeado desde Accounts.API)
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)           // carpeta donde está el exe
            .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        // 2) Registrar servicios (DbContext usando ConnectionStrings:Fintech)
        var services = new ServiceCollection();

        var connectionString = configuration.GetConnectionString("Fintech")
            ?? throw new InvalidOperationException("ConnectionString 'Fintech' not found in appsettings.Development.json.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        var serviceProvider = services.BuildServiceProvider();

        // 3) Inicializar TransferTools con el ServiceProvider (para crear DbContext por scope)
        TransferTools.Init(serviceProvider);

        // 4) Registrar la clase de tools MCP y arrancar el servidor MCP por STDIO
        MCPServer.Register<TransferTools>();

        await MCPServer.StartAsync("FintechAccountsTools", "1.0.0");
    }
}

