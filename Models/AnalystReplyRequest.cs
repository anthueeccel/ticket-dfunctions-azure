/// <summary>
/// Payload sent to <c>POST /api/tickets/{instanceId}/reply/{analystId}</c> by a
/// (simulated) analyst. Simulates the yes/no answer a human analyst would send
/// by email.
/// </summary>
public class AnalystReplyRequest
{
    /// <summary>true = analyst takes the ticket, false = analyst declines it.</summary>
    public bool Accepted { get; set; }
}