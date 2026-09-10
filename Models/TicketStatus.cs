/// <summary>
/// Lifecycle states a support ticket can be in. The orchestrator moves a ticket
/// through these states as the workflow progresses.
/// </summary>
public enum TicketStatus
{
    /// <summary>Ticket was persisted but no analyst has been notified yet.</summary>
    New,

    /// <summary>Analysts have been (or are being) notified and we are waiting for replies.</summary>
    WaitingForAnalyst,

    /// <summary>An analyst accepted the ticket and it was assigned to them.</summary>
    Assigned,

    /// <summary>Not assigned; manager escalation is in progress.</summary>
    Escalated,

    /// <summary>
    /// Terminal state after the closure activity ran. Whether the ticket completed
    /// normally or ended without an owner is recorded in <see cref="ClosureReason"/>.
    /// </summary>
    Closed
}