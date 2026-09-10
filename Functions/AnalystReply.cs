using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP trigger that simulates an analyst replying to a ticket notification.
/// </summary>
/// <remarks>
/// In a real system the analyst would reply by email; here the reply comes
/// through an HTTP endpoint that raises a durable <b>external event</b> into the
/// waiting orchestration instance. The event name is per-analyst
/// (<c>AnalystReply-{analystId}</c>) so no extra correlation logic is needed.
/// </remarks>
public class AnalystReply
{
    /// <summary>
    /// <c>POST /api/tickets/{instanceId}/reply/{analystId}</c> — raises the
    /// <c>AnalystReply-{analystId}</c> external event into the orchestration.
    /// </summary>
    [Function("AnalystReply")]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tickets/{instanceId}/reply/{analystId}")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        string analystId)
    {
        ILogger logger = req.FunctionContext.GetLogger("AnalystReply");

        try
        {
            AnalystReplyRequest? reply = await req.ReadFromJsonAsync<AnalystReplyRequest>();

            if (reply is null)
            {
                HttpResponseData bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Request body must be a JSON object with an 'accepted' boolean field.");
                return bad;
            }

            string eventName = $"AnalystReply-{analystId}";

            // Raise the external event carrying the bool Accepted value.
            await client.RaiseEventAsync(
                instanceId,
                eventName,
                reply.Accepted,
                CancellationToken.None);

            logger.LogInformation(
                "Raised event '{EventName}' on instance {InstanceId}: analyst {AnalystId} {Decision}.",
                eventName, instanceId, analystId,
                reply.Accepted ? "ACCEPTED" : "DECLINED");

            HttpResponseData ok = req.CreateResponse(HttpStatusCode.Accepted);
            await ok.WriteStringAsync(
                $"Reply received: analyst {analystId} {(reply.Accepted ? "accepted" : "declined")}.");
            return ok;
        }
        catch (JsonException jex)
        {
            logger.LogError(jex, "Invalid JSON in AnalystReply request body.");
            HttpResponseData bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON in request body.");
            return bad;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process analyst reply for instance {InstanceId}, analyst {AnalystId}.",
                instanceId, analystId);
            HttpResponseData bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync($"Error: {ex.Message}");
            return bad;
        }
    }
}