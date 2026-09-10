using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP trigger that starts a new support-ticket orchestration.
/// </summary>
/// <remarks>
/// This is the <b>client</b> in the Durable Functions pattern: it receives a
/// <see cref="TicketInput"/> via HTTP, schedules a new <c>TicketOrchestrator</c>
/// instance, and returns the standard Durable Functions 202 status-check response
/// (built-in — no custom status endpoint needed).
/// </remarks>
public class CreateTicket
{
    /// <summary>
    /// <c>POST /api/tickets</c> — creates a new ticket orchestration.
    /// </summary>
    [Function("CreateTicket")]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tickets")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        ILogger logger = req.FunctionContext.GetLogger("CreateTicket");

        try
        {
            // Deserialize the body with string-enum support (e.g. "Hardware" → TicketCategory.Hardware).
            string requestBody = await req.ReadAsStringAsync();

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                                Converters = { new JsonStringEnumConverter() }
            };

            TicketInput? input = JsonSerializer.Deserialize<TicketInput>(requestBody, jsonOptions);

            if (input is null || string.IsNullOrWhiteSpace(input.Title))
            {
                HttpResponseData bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Request body must contain a non-empty 'title' field.");
                return bad;
            }

            // Start a new orchestration instance. Returns the generated instance ID string.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                "TicketOrchestrator", input);

            logger.LogInformation(
                "Started TicketOrchestrator instance {InstanceId} for ticket '{Title}'.",
                instanceId, input.Title);

            // Build the standard 202 response with status-query and send-event URIs.
            return client.CreateCheckStatusResponse(req, instanceId);
        }
        catch (JsonException jex)
        {
            logger.LogError(jex, "Invalid JSON in CreateTicket request body.");
            HttpResponseData bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON in request body.");
            return bad;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create ticket orchestration.");
            HttpResponseData bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync($"Error: {ex.Message}");
            return bad;
        }
    }
}