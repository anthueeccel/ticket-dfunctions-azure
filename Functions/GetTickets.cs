using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP triggers for reading tickets persisted in the <c>Tickets</c> Table Storage table.
/// </summary>
/// <remarks>
/// <para>
/// <c>GET /api/tickets</c> lists tickets with optional filters and pagination:
/// <c>?status=Closed&amp;closureReason=Unassigned&amp;skip=0&amp;top=25</c>. All query
/// values are case-insensitive; enum values serialize as strings for readability.
/// </para>
/// <para><c>GET /api/tickets/{ticketId}</c> returns a single ticket, or 404 if not found.</para>
/// </remarks>
public class GetTickets
{
    private const int DefaultTop = 100;
    private const int MaxTop = 200;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// <c>GET /api/tickets[?status=&amp;closureReason=&amp;skip=&amp;top=]</c> — lists tickets
    /// with optional case-insensitive filters and pagination.
    /// </summary>
    [Function("GetTickets")]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tickets")]
        HttpRequestData req)
    {
        ILogger logger = req.FunctionContext.GetLogger("GetTickets");

        string? statusFilter = req.Query["status"];
        string? closureReasonFilter = req.Query["closureReason"];
        string? skipRaw = req.Query["skip"];
        string? topRaw = req.Query["top"];

        try
        {
            int skip = ParsePagingValue(skipRaw, "skip");
            int top = ParsePagingValue(topRaw, "top", DefaultTop, MaxTop);

            List<Ticket> tickets = await TableStorageService.ListTickets(
                statusFilter, closureReasonFilter, skip, top);

            HttpResponseData ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(tickets, JsonOptions));

            logger.LogInformation(
                "Listed {Count} ticket(s) (status={Status}, closureReason={ClosureReason}, skip={Skip}, top={Top}).",
                tickets.Count,
                string.IsNullOrEmpty(statusFilter) ? "<any>" : statusFilter,
                string.IsNullOrEmpty(closureReasonFilter) ? "<any>" : closureReasonFilter,
                skip, top);
            return ok;
        }
        catch (ArgumentException aex)
        {
            HttpResponseData bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync(
                $"{aex.Message} Valid status values: {string.Join(", ", Enum.GetNames<TicketStatus>())}. " +
                $"Valid closureReason values: {string.Join(", ", Enum.GetNames<ClosureReason>())}.");
            return bad;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list tickets (status={Status}, closureReason={ClosureReason}).",
                statusFilter, closureReasonFilter);
            HttpResponseData err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteStringAsync($"Error: {ex.Message}");
            return err;
        }
    }

    /// <summary>
    /// <c>GET /api/tickets/{ticketId}</c> — returns a single ticket, or 404 if not found.
    /// </summary>
    [Function("GetTicketById")]
    public static async Task<HttpResponseData> RunById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tickets/{ticketId}")]
        HttpRequestData req,
        string ticketId)
    {
        ILogger logger = req.FunctionContext.GetLogger("GetTicketById");

        try
        {
            Ticket ticket = await TableStorageService.LoadTicket(ticketId);

            HttpResponseData ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(ticket, JsonOptions));
            return ok;
        }
        catch (RequestFailedException rfe) when (rfe.Status == 404)
        {
            HttpResponseData notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"Ticket '{ticketId}' was not found.");
            return notFound;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load ticket {TicketId}.", ticketId);
            HttpResponseData err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteStringAsync($"Error: {ex.Message}");
            return err;
        }
    }

    /// <summary>Parses a paging query value; returns <paramref name="defaultTop"/> when absent.</summary>
    /// <exception cref="ArgumentException">Thrown when the value is non-numeric, negative, or above <paramref name="maxTop"/>.</exception>
    private static int ParsePagingValue(string? raw, string name, int? defaultTop = null, int? maxTop = null)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return defaultTop ?? 0;
        }
        if (!int.TryParse(raw, out int value) || value < 0)
        {
            throw new ArgumentException($"'{name}' must be a non-negative integer (got '{raw}').");
        }
        if (maxTop.HasValue && value > maxTop.Value)
        {
            throw new ArgumentException($"'{name}' must not exceed {maxTop.Value} (got '{raw}').");
        }
        return value;
    }
}