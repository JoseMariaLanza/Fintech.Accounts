using Accounts.Domain.Accounts.Entities;
using Accounts.Infrastructure.Persistence;
using Fintech.Shared.Events;                  // TransferCompleted
using MCPSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Fintech.Accounts.McpServer;

public class TransferTools
{
    private static IServiceProvider? _serviceProvider;

    // Lo llama Program.Main una sola vez
    public static void Init(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static AppDbContext CreateDbContext()
    {
        if (_serviceProvider is null)
            throw new InvalidOperationException("TransferTools.Init() no fue llamado. Inicializa en Program.Main.");

        // Creamos scope por cada tool call, como en un controller
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [McpTool(
    Name = "export_successful_transfers",
    Description = "Exporta un CSV con las transferencias exitosas (OutboxMessages con Status=Sent y Type=TransferCompleted) en un rango de fechas.")]
    public static string ExportSuccessfulTransfers(
    [McpParameter(true, Description = "Fecha desde (UTC, formato yyyy-MM-dd).")]
    string fromDate,
    [McpParameter(true, Description = "Fecha hasta (UTC, formato yyyy-MM-dd).")]
    string toDate)
    {
        var from = DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal).ToUniversalTime();

        var to = DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal).ToUniversalTime();

        using var db = CreateDbContext();

        var query = db.OutboxMessages
            .Where(m =>
                m.Type == nameof(TransferCompleted) &&
                m.CreatedAtUtc >= from &&
                m.CreatedAtUtc <= to)
            .OrderBy(m => m.CreatedAtUtc)
            .AsNoTracking();

        // Si querés limitar igual, hardcodeá un límite razonable:
        // const int MaxRows = 1000;
        // query = query.Take(MaxRows);

        var rows = query.ToList();

        var sb = new StringBuilder();
        sb.AppendLine("TransferId,FromAccountId,ToAccountId,Amount,OccurredAtUtc,RoutingKey,CreatedAtUtc,SentAtUtc,RetryCount");

        foreach (var msg in rows)
        {
            var evt = JsonSerializer.Deserialize<TransferCompleted>(msg.Payload.RootElement);
            if (evt is null) continue;

            sb.AppendLine(string.Join(",",
                evt.TransferId,
                evt.FromAccountId,
                evt.ToAccountId,
                evt.Amount.ToString(CultureInfo.InvariantCulture),
                evt.OccurredAt.ToUniversalTime().ToString("O"),
                msg.RoutingKey,
                msg.CreatedAtUtc.ToString("O"),
                msg.SentAtUtc?.ToString("O") ?? "",
                msg.RetryCount));
        }

        return sb.ToString();
    }

    [McpTool(
    Name = "summarize_transfers_for_accountant",
    Description = "Genera un resumen en texto/Markdown para un contador a partir del CSV de transferencias.")]
    public static string SummarizeTransfersForAccountant(
    [McpParameter(true, Description = "CSV generado por export_successful_transfers.")] string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return "El CSV está vacío o no se recibió contenido.";
        }

        // Normalizar fin de línea para que StringReader funcione bien con n8n
        var normalized = csvContent
            .Replace("\\r\\n", "\n")   // texto "\r\n"
            .Replace("\\n", "\n")      // texto "\n"
            .Replace("\r\n", "\n")     // CRLF real
            .Replace("\r", "\n");      // solo CR, por si acaso

        using var reader = new StringReader(normalized);

        // Header
        string? line = reader.ReadLine();
        if (line is null)
        {
            return "El CSV no contiene ni siquiera una fila de encabezado.";
        }

        decimal total = 0;
        int count = 0;
        var perAccount = new Dictionary<Guid, decimal>();

        // Para poder listar todas las transferencias
        var transfers = new List<TransferRow>();

        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            // Esperamos 9 columnas:
            // 0:TransferId,1:FromAccountId,2:ToAccountId,3:Amount,
            // 4:OccurredAtUtc,5:RoutingKey,6:CreatedAtUtc,7:SentAtUtc,8:RetryCount
            if (parts.Length < 9)
                continue;

            if (!Guid.TryParse(parts[0], out var transferId))
                continue;
            if (!Guid.TryParse(parts[1], out var fromAccountId))
                continue;
            if (!Guid.TryParse(parts[2], out var toAccountId))
                continue;
            if (!decimal.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                continue;

            // Formato "O" porque en ExportSuccessfulTransfers usaste ToString("O")
            if (!DateTime.TryParseExact(parts[4], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var occurredAtUtc))
                continue;

            var routingKey = parts[5];

            if (!DateTime.TryParseExact(parts[6], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var createdAtUtc))
                continue;

            DateTime? sentAtUtc = null;
            if (!string.IsNullOrEmpty(parts[7]))
            {
                if (DateTime.TryParseExact(parts[7], "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var parsedSent))
                {
                    sentAtUtc = parsedSent;
                }
            }

            int retryCount = 0;
            _ = int.TryParse(parts[8], NumberStyles.Any, CultureInfo.InvariantCulture, out retryCount);

            // Agregados
            total += amount;
            count++;

            if (!perAccount.ContainsKey(fromAccountId)) perAccount[fromAccountId] = 0;
            if (!perAccount.ContainsKey(toAccountId)) perAccount[toAccountId] = 0;

            perAccount[fromAccountId] -= amount;
            perAccount[toAccountId] += amount;

            // Guardar detalle
            transfers.Add(new TransferRow(
                transferId,
                fromAccountId,
                toAccountId,
                amount,
                occurredAtUtc,
                routingKey,
                createdAtUtc,
                sentAtUtc,
                retryCount));
        }

        if (count == 0)
        {
            return "El CSV no contiene filas de transferencias válidas.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("# Resumen de transferencias para contabilidad");
        sb.AppendLine();
        sb.AppendLine($"- Cantidad de transferencias: **{count}**");
        sb.AppendLine($"- Monto total movido: **{total.ToString("N2", CultureInfo.InvariantCulture)}**");
        sb.AppendLine();
        sb.AppendLine("## Saldos netos por cuenta (negativo = salió dinero, positivo = entró)");
        foreach (var kvp in perAccount.OrderByDescending(k => k.Value))
        {
            sb.AppendLine($"- Cuenta `{kvp.Key}`: {kvp.Value.ToString("N2", CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine();
        sb.AppendLine("## Detalle de transferencias");
        sb.AppendLine();

        foreach (var t in transfers.OrderBy(t => t.OccurredAtUtc))
        {
            sb.AppendLine($"- **TransferId**: `{t.TransferId}`");
            sb.AppendLine($"  - FromAccountId : `{t.FromAccountId}`");
            sb.AppendLine($"  - ToAccountId   : `{t.ToAccountId}`");
            sb.AppendLine($"  - Amount        : **{t.Amount.ToString("N2", CultureInfo.InvariantCulture)}**");
            sb.AppendLine($"  - OccurredAtUtc : {t.OccurredAtUtc:O}");
            sb.AppendLine($"  - RoutingKey    : `{t.RoutingKey}`");
            sb.AppendLine($"  - CreatedAtUtc  : {t.CreatedAtUtc:O}");
            sb.AppendLine($"  - SentAtUtc     : {(t.SentAtUtc.HasValue ? t.SentAtUtc.Value.ToString("O") : "(sin enviar)")}");
            sb.AppendLine($"  - RetryCount    : {t.RetryCount}");
            sb.AppendLine();
        }

        sb.AppendLine("_Generado automáticamente por un agente MCP conectado a Fintech.Accounts._");

        return sb.ToString();
    }

    // Pequeño record interno para el detalle
    private sealed record TransferRow(
        Guid TransferId,
        Guid FromAccountId,
        Guid ToAccountId,
        decimal Amount,
        DateTime OccurredAtUtc,
        string RoutingKey,
        DateTime CreatedAtUtc,
        DateTime? SentAtUtc,
        int RetryCount);

}
