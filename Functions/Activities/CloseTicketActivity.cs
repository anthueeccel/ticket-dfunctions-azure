using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

/// <summary>
/// Terminal activity that closes a ticket: sets <see cref="TicketStatus.Closed"/>, stamps
/// <c>ClosedAt</c> and persists the row (including <c>FinalPrice</c> if pricing ran).
/// </summary>
/// <remarks>
/// Every orchestration path converges here — this demonstrates fan-in at the end of the
/// workflow. For tickets that were never assigned (both rounds failed), <c>Unassigned = true</c>
/// so it logs a clear reason, and the final stored status is <see cref="TicketStatus.Closed"/>.
/// </remarks>
public class CloseTicketActivity
{
    [Function("CloseTicketActivity")]
    public static async Task<Ticket> Run(
        [ActivityTrigger] CloseInput input,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger("CloseTicketActivity");

        Ticket ticket = await TableStorageService.LoadTicket(input.TicketId);
        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = input.ClosedAt;
        // Terminal status stays Closed; ClosureReason distinguishes a completed
        // ticket from one that ended without an owner (both rounds failed).
        ticket.ClosureReason = input.Unassigned ? ClosureReason.Unassigned : ClosureReason.Completed;
        await TableStorageService.UpdateTicket(ticket);

        if (input.Unassigned)
        {
            logger.LogInformation("Closed ticket {ticketId} WITHOUT an owner (both rounds failed): status={status}, closureReason={reason}.",
                input.TicketId, ticket.Status, ticket.ClosureReason);
        }
        else
        {
            logger.LogInformation("Closed ticket {ticketId}: status={status}, closureReason={reason}, hours={hours}, finalPrice={price}.",
                input.TicketId, ticket.Status, ticket.ClosureReason, ticket.HoursSpent, ticket.FinalPrice);
        }
        return ticket;
    }
}

/// <summary>Input for <see cref="CloseTicketActivity"/>.</summary>
public class CloseInput
{
    public string TicketId { get; set; }
    public bool Unassigned { get; set; }
    public DateTime ClosedAt { get; set; }

    public CloseInput()
    {
    }

    public CloseInput(string ticketId, bool unassigned, DateTime closedAt)
    {
        TicketId = ticketId;
        Unassigned = unassigned;
        ClosedAt = closedAt;
    }
}