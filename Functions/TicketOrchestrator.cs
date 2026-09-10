using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrator function that implements the complete support-ticket routing workflow.
/// </summary>
/// <remarks>
/// <para>This orchestrator demonstrates three Durable Functions patterns:</para>
/// <list type="bullet">
///   <item>
///     <description><b>Fan-out / fan-in:</b> step 3a calls <see cref="NotifyAnalystActivity"/>
///     in parallel for all five analysts via <see cref="Task.WhenAll{T}"/>, then continues only
///     after every notification has landed.</description>
///   </item>
///   <item>
///     <description><b>External-event + durable-timer race:</b> step 3c creates a durable
///     timer (10 min round 1, 5 min round 2) and races it against per-analyst
///     <see cref="TaskOrchestrationContext.WaitForExternalEvent{T}"/> calls using
///     <see cref="Task.WhenAny{T}"/>.</description>
///   </item>
///   <item>
///     <description><b>Activity chaining:</b> steps 1–5 form a chain of activity calls that
///     persist each state change to Table Storage. All non-deterministic operations
///     (Guid generation, clock reads, random numbers) happen inside activities so the
///     orchestrator stays replay-safe.</description>
///   </item>
/// </list>
/// <para>
/// Flow: 1️⃣ persist ticket (New) → 2️⃣ up to 2 rounds of notify/wait/escalate →
/// 3️⃣ (if assigned) calculate price → 4️⃣ close ticket.
/// </para>
/// </remarks>
public class TicketOrchestrator
{
    /// <summary>
    /// Entry point for the ticket routing orchestration.
    /// </summary>
    [Function("TicketOrchestrator")]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        TicketInput input)
    {
        ILogger logger = context.CreateReplaySafeLogger("TicketOrchestrator");
        var opts = new TaskOptions(); // no retry policy for this study project

        // ---- Step 1: generate ticket id (deterministic — replay-safe Guid) ----
        string ticketId = context.NewGuid().ToString();
        logger.LogInformation(
            "Orchestration {InstanceId}: starting ticket workflow for '{Title}' (ticketId={TicketId})",
            context.InstanceId, input.Title, ticketId);

        // ---- Step 2: persist the new ticket (status = New) ----
        Ticket ticket = new Ticket
        {
            TicketId = ticketId,
            Title = input.Title,
            Description = input.Description,
            Category = input.Category,
            Priority = input.Priority,
            Status = TicketStatus.New,
            CreatedAt = context.CurrentUtcDateTime
                };

        ticket = await context.CallActivityAsync<Ticket>("SaveTicketActivity", ticket, opts);

        bool assigned = false;
        string? assignedAnalystId = null;

        // ---- Step 3: up to 2 rounds of notify → wait → escalate ----
        for (int round = 1; round <= 2 && !assigned; round++)
        {
            logger.LogInformation("Round {Round} — notifying {Count} analysts for ticket {TicketId}",
                round, SampleAnalysts.All.Count, ticketId);

            // ---- 3a. FAN-OUT / FAN-IN: notify all analysts in parallel ----
            // Each CallActivityAsync schedules a separate sub-task; Task.WhenAll fans in
            // so the orchestration only resumes after every notification completed.
            Task<string>[] notifyTasks = SampleAnalysts.All.Select(a =>
                context.CallActivityAsync<string>(
                    "NotifyAnalystActivity",
                    new NotifyInput(ticketId, a.AnalystId),
                    opts))
                .ToArray();

            await Task.WhenAll(notifyTasks);

            // Analysts are now notified and we are waiting for replies. Persist the
            // wait-state so the status trail reads New → WaitingForAnalyst → (Escalated)
            // → WaitingForAnalyst → Closed instead of staying on Escalated in round 2.
            await context.CallActivityAsync<Ticket>(
                "SetTicketStatusActivity",
                new SetTicketStatusInput(ticketId, TicketStatus.WaitingForAnalyst),
                opts);

            // ---- 3b. Durable timer (10 min round 1, 5 min round 2) ----
            TimeSpan timeout = TimeSpan.FromMinutes(round == 1 ? 10 : 5);
            using var timerCts = new CancellationTokenSource();
            Task timerTask = context.CreateTimer(timeout, timerCts.Token);

            // ---- 3c. Race the timer against per-analyst external-event waits ----
            // One WaitForExternalEvent<bool> per analyst. Event names are unique
            // (AnalystReply-A1 … AnalystReply-A5) so the HTTP endpoint needs no
            // extra correlation — it just raises the right event name.
            var pendingReplies = new Dictionary<string, Task<bool>>();
            foreach (var analyst in SampleAnalysts.All)
            {
                string eventName = $"AnalystReply-{analyst.AnalystId}";
                pendingReplies[analyst.AnalystId] =
                    context.WaitForExternalEvent<bool>(eventName, CancellationToken.None);
            }

            while (pendingReplies.Count > 0 && !timerTask.IsCompleted)
            {
                // Combine timer + all pending reply tasks into one WhenAny race.
                // Task<bool> is cast to Task so the collection is homogeneous.
                Task[] allTasks = pendingReplies.Values
                    .Cast<Task>()
                    .Append(timerTask)
                    .ToArray();

                Task completedTask = await Task.WhenAny(allTasks);

                if (completedTask == timerTask)
                {
                    logger.LogInformation("Round {Round} timed out after {Timeout}.", round, timeout);
                    break;
                }

                // An external event fired — identify which analyst replied.
                string repliedAnalystId = pendingReplies
                    .FirstOrDefault(kvp => (Task)kvp.Value == completedTask).Key;

                bool accepted = pendingReplies[repliedAnalystId].Result;
                pendingReplies.Remove(repliedAnalystId);

                if (accepted)
                {
                    timerCts.Cancel();
                    logger.LogInformation(
                        "Analyst {AnalystId} ACCEPTED ticket {TicketId} in round {Round}.",
                        repliedAnalystId, ticketId, round);

                    await context.CallActivityAsync<Ticket>(
                        "AssignOwnerActivity",
                        new AssignOwnerInput(ticketId, repliedAnalystId),
                        opts);

                    assignedAnalystId = repliedAnalystId;
                    assigned = true;
                    break; // jump straight to step 4
                }
                else
                {
                    logger.LogInformation(
                        "Analyst {AnalystId} DECLINED ticket {TicketId}; {Remaining} still pending.",
                        repliedAnalystId, ticketId, pendingReplies.Count);

                    if (pendingReplies.Count == 0)
                    {
                        logger.LogInformation("All analysts declined in round {Round}.", round);
                        break; // round failed early
                    }
                }
            }

            // ---- 3d. Handle round failure ----
            if (!assigned)
            {
                await context.CallActivityAsync<string>(
                    "NotifyManagerActivity",
                    new NotifyManagerInput(ticketId, round),
                    opts);

                if (round == 2)
                {
                    logger.LogInformation(
                        "Both rounds failed for ticket {TicketId}; closing as UNASSIGNED.", ticketId);
                    await context.CallActivityAsync<Ticket>(
                        "CloseTicketActivity",
                        new CloseInput(ticketId, true, context.CurrentUtcDateTime),
                        opts);
                    return; // end orchestration
                }

                logger.LogInformation("Round {Round} failed; continuing to round {NextRound}.",
                    round, round + 1);
            }
        }

        // ---- Steps 4 & 5: only if an analyst was assigned ----
        if (assigned)
        {
            double finalPrice = await context.CallActivityAsync<double>(
                "CalculateFinalPriceActivity",
                ticketId,
                opts);
            logger.LogInformation(
                "Calculated final price {FinalPrice} for ticket {TicketId} (owner={Owner}).",
                finalPrice, ticketId, assignedAnalystId);

            await context.CallActivityAsync<Ticket>(
                "CloseTicketActivity",
                new CloseInput(ticketId, false, context.CurrentUtcDateTime),
                opts);
            logger.LogInformation("Ticket {TicketId} closed successfully.", ticketId);
        }
    }
}
