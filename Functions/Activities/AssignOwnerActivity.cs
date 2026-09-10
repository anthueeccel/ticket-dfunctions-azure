using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

/// <summary>
/// Activity that assigns a ticket to the analyst who accepted it, and simulates logging hours.
/// </summary>
/// <remarks>
/// Demonstrates single-purpose activities doing the mutation the orchestrator must not do
/// (randomness / domain side-effects). The <see cref="HoursSpent"/> value is fabricated here
/// — a stand-in for a real "analyst logs hours" step — using a deterministic value derived from
/// the ticket/analyst ids (1..8, one decimal) so replay stays reproducible.
/// </remarks>
public class AssignOwnerActivity
{
    [Function("AssignOwnerActivity")]
    public static async Task<Ticket> Run(
        [ActivityTrigger] AssignOwnerInput input,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger("AssignOwnerActivity");

        Ticket ticket = await TableStorageService.LoadTicket(input.TicketId);
        ticket.Status = TicketStatus.Assigned;
        ticket.AssignedAnalystId = input.AnalystId;

        // STAND-IN for a real "log hours" step (no hours field exists on AnalystReply).
        ticket.HoursSpent = SimulatedHours(input.TicketId, input.AnalystId);

        await TableStorageService.UpdateTicket(ticket);

        logger.LogInformation("Assigned ticket {ticketId} to analyst {analystId}; simulated {hours} hours logged.",
            input.TicketId, input.AnalystId, ticket.HoursSpent);
        return ticket;
    }

    /// <summary>Deterministic pseudo-random value in [1.0 .. 8.0] rounded to one decimal.</summary>
    private static double SimulatedHours(string ticketId, string analystId)
    {
        int seed = (ticketId + analystId).GetHashCode() & 0x7fffffff;
        double hours = 1.0 + (seed % 8);      // 1 .. 8
        hours = Math.Round(hours, 1);          // already whole, kept for clarity/rounding contract
        return hours;
    }
}

/// <summary>Input for <see cref="AssignOwnerActivity"/>.</summary>
public class AssignOwnerInput
{
    public string TicketId { get; set; }
    public string AnalystId { get; set; }

    public AssignOwnerInput()
    {
    }

    public AssignOwnerInput(string ticketId, string analystId)
    {
        TicketId = ticketId;
        AnalystId = analystId;
    }
}