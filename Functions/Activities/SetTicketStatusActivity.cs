using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

/// <summary>
/// Small activity that sets a ticket's <see cref="TicketStatus"/> in storage. Used by the
/// orchestrator to reflect the wait-state transitions (e.g. back to
/// <see cref="TicketStatus.WaitingForAnalyst"/> when a new escalation round begins), keeping
/// all storage I/O inside activities so the orchestrator stays replay-safe.
/// </summary>
public class SetTicketStatusActivity
{
    [Function("SetTicketStatusActivity")]
    public static async Task<Ticket> Run(
        [ActivityTrigger] SetTicketStatusInput input,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger("SetTicketStatusActivity");

        Ticket ticket = await TableStorageService.LoadTicket(input.TicketId);
        ticket.Status = input.Status;
        await TableStorageService.UpdateTicket(ticket);

        logger.LogInformation("Ticket {ticketId} status set to {status}.", input.TicketId, ticket.Status);
        return ticket;
    }
}

/// <summary>Input for <see cref="SetTicketStatusActivity"/>: which ticket, which status.</summary>
public class SetTicketStatusInput
{
    public string TicketId { get; set; }
    public TicketStatus Status { get; set; }

    public SetTicketStatusInput()
    {
    }

    public SetTicketStatusInput(string ticketId, TicketStatus status)
    {
        TicketId = ticketId;
        Status = status;
    }
}