using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

/// <summary>
/// Activity that (simulated) escalates a ticket to a manager when a round times out.
/// </summary>
/// <remarks>
/// This hooks into the external-event + timer pattern: after a round of analyst replies
/// fails (either all declined or a timeout), the orchestrator chains here. It marks the
/// ticket as <see cref="TicketStatus.Escalated"/> in storage and logs a simulated notice,
/// demonstrating activity CHAINING after a failed wait.
/// </remarks>
public class NotifyManagerActivity
{
    [Function("NotifyManagerActivity")]
    public static async Task<string> Run(
        [ActivityTrigger] NotifyManagerInput input,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger("NotifyManagerActivity");

        // Reflect the escalation in storage so the status trail is visible.
        Ticket ticket = await TableStorageService.LoadTicket(input.TicketId);
        ticket.Status = TicketStatus.Escalated;
        await TableStorageService.UpdateTicket(ticket);

        // No real email integration — simulate the manager notification.
        logger.LogInformation("[SIMULATED ESCALATION] Round {round} failed for ticket {ticketId}; manager notified (status={status}).",
            input.Round, input.TicketId, ticket.Status);

        return "Escalated-round" + input.Round;
    }
}

/// <summary>Input for <see cref="NotifyManagerActivity"/>.</summary>
public class NotifyManagerInput
{
    public string TicketId { get; set; }
    public int Round { get; set; }

    public NotifyManagerInput()
    {
    }

    public NotifyManagerInput(string ticketId, int round)
    {
        TicketId = ticketId;
        Round = round;
    }
}