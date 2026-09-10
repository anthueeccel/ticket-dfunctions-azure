using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

/// <summary>
/// Activity that computes and persists the final price of an assigned ticket.
/// </summary>
/// <remarks>
/// Demonstrates activity CHAINING + delegation of pure business logic: the pricing
/// formula lives in the deterministic <see cref="PricingTable"/> helper; this activity only
/// loads the ticket, feeds it the price, and persists the result. Because it only ever runs
/// after an analyst has accepted, it is safe to use the assigned hours recorded earlier.
/// </remarks>
public class CalculateFinalPriceActivity
{
    [Function("CalculateFinalPriceActivity")]
    public static async Task<Double> Run(
        [ActivityTrigger] string ticketId,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger("CalculateFinalPriceActivity");

        Ticket ticket = await TableStorageService.LoadTicket(ticketId);
        double hours = (ticket.HoursSpent == null ? 0.0 : (double)ticket.HoursSpent);

        double finalPrice = PricingTable.CalculateFinalPrice(ticket.Category, ticket.Priority, hours);
        ticket.FinalPrice = (decimal)finalPrice;

        await TableStorageService.UpdateTicket(ticket);
        logger.LogInformation("Computed final price {price} for ticket {ticketId} ({hours} h, {category}/{priority}).",
            finalPrice, ticketId, hours, ticket.Category, ticket.Priority);
        return finalPrice;
    }
}