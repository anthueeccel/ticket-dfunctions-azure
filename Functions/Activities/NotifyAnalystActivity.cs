using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

/// <summary>
/// Activity that (simulated) emails a single analyst about a ticket.
/// </summary>
/// <remarks>
/// This is the unit-of-work executed by the FAN-OUT/FAN-IN step. The orchestrator calls it
/// once per analyst using <c>Task.WhenAll(...)</c>, so all five notifications run in parallel;
/// the orchestrator then fans-in by awaiting the combined task before starting the wait loop.
/// There is no real email integration — the "send" is just a log line (and optionally the start
/// of an email pipeline in a real system).
/// </remarks>
public class NotifyAnalystActivity
{
    [Function("NotifyAnalystActivity")]
    public static string Run(
        [ActivityTrigger] NotifyInput input,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger("NotifyAnalystActivity");
        string analystName = LookupName(input.AnalystId);

        // No real SMTP/email provider — we simulate the outbound notification.
        logger.LogInformation("[SIMULATED EMAIL] Sent 'ticket {ticketId} needs attention' to analyst {analyst} <{analystId}>",
            input.TicketId, analystName, input.AnalystId);

        return "Notified-" + input.AnalystId;
    }

    private static string LookupName(string analystId)
    {
        foreach (Analyst analyst in SampleAnalysts.All)
        {
            if (analyst.AnalystId.Equals(analystId))
            {
                return analyst.Name;
            }
        }
        return analystId;
    }
}

/// <summary>Input for <see cref="NotifyAnalystActivity"/>: which analyst to notify about which ticket.</summary>
public class NotifyInput
{
    public string TicketId { get; set; }
    public string AnalystId { get; set; }

    public NotifyInput()
    {
    }

    public NotifyInput(string ticketId, string analystId)
    {
        TicketId = ticketId;
        AnalystId = analystId;
    }
}