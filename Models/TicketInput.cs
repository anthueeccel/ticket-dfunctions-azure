/// <summary>
/// Payload sent by the HTTP client to <c>POST /api/tickets</c>. This is the
/// input that starts a new orchestration instance via
/// <c>ScheduleNewOrchestrationInstanceAsync</c>.
/// </summary>
public class TicketInput
{
        public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketCategory Category { get; set; }
    public TicketPriority Priority { get; set; }
}