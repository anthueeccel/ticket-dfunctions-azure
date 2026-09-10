using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

/// <summary>
/// Activity that persists a brand-new ticket (status = <see cref="TicketStatus.New"/>)
/// to the <c>Tickets</c> table.
/// </summary>
/// <remarks>
/// This is a starter activity in the orchestration CHAIN: the orchestrator hands
/// off the fully-populated <see cref="Ticket"/> and waits for this to complete before
/// fanning-out the notifications. It is deterministic — no randomness or clock reads here.
/// </remarks>
public class SaveTicketActivity
{
    [Function("SaveTicketActivity")]
    public static async Task<Ticket> Run(
        [ActivityTrigger] Ticket ticket,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger("SaveTicketActivity");
        await TableStorageService.SaveTicket(ticket);
        logger.LogInformation("Saved ticket {ticketId} with initial status {status}", ticket.TicketId, ticket.Status);
        return ticket;
    }
}