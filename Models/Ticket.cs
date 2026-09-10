/// <summary>
/// Terminal reason recorded when a ticket reaches <see cref="TicketStatus.Closed"/>.
/// Kept as a separate field so the single terminal status <c>Closed</c> can be
/// interpreted (e.g. a ticket closed because every analyst declined is
/// <see cref="ClosureReason.Unassigned"/> even though its status is <c>Closed</c>).
/// </summary>
public enum ClosureReason
{
    /// <summary>Ticket has not been closed yet.</summary>
    None,

    /// <summary>An analyst accepted and the ticket completed normally (pricing ran).</summary>
    Completed,

    /// <summary>Both analyst rounds failed; the ticket closed without an owner (pricing skipped).</summary>
    Unassigned
}

/// <summary>
/// Mutable aggregate representing a support ticket as persisted in Azure Table
/// Storage (the <c>Tickets</c> table).
/// </summary>
/// <remarks>
/// The model uses plain .NET types (<see cref="DateTime"/>, <see cref="decimal"/>,
/// <see cref="double"/>, <see cref="string"/>). Azure Table Storage has no native
/// <c>decimal</c>, so <see cref="FinalPrice"/> is persisted as a <c>double</c> column and the
/// timestamp fields as ISO-8601 strings; <c>Services/TableStorageService</c> handles the mapping.
/// </remarks>
public class Ticket
{
        public string TicketId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketCategory Category { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public string? AssignedAnalystId { get; set; }
    public double? HoursSpent { get; set; }
    public decimal? FinalPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>Why the ticket reached <see cref="TicketStatus.Closed"/>; <see cref="ClosureReason.None"/> while open.</summary>
    public ClosureReason ClosureReason { get; set; } = ClosureReason.None;
}