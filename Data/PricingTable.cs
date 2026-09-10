using System.Collections.Generic;

/// <summary>
/// Static pricing rules used by <c>CalculateFinalPriceActivity</c>.
/// </summary>
/// <remarks>
/// Formula: <c>FinalPrice = (basePrice * priorityMultiplier) + (HoursSpent * hourlyRate)</c>.
/// Everything here is deterministic (pure data), so activity output stays replay-safe.
/// </remarks>
public static class PricingTable
{
    public static readonly double HourlyRate = 25.0;

    public static readonly Dictionary<TicketCategory, double> BasePrices =
        new Dictionary<TicketCategory, double>()
        {
            { TicketCategory.Hardware, 50 },
            { TicketCategory.Software, 30 },
            { TicketCategory.Network, 70 },
            { TicketCategory.Access, 20 },
            { TicketCategory.Other, 40 }
        };

    public static readonly Dictionary<TicketPriority, double> PriorityMultipliers =
        new Dictionary<TicketPriority, double>()
        {
            { TicketPriority.Low, 1.0 },
            { TicketPriority.Medium, 1.3 },
            { TicketPriority.High, 1.6 },
            { TicketPriority.Critical, 2.0 }
        };

    /// <summary>Computes the final price for a category, priority and time spent.</summary>
    public static double CalculateFinalPrice(TicketCategory category, TicketPriority priority, double hoursSpent)
    {
        double basePrice = BasePrices[category];
        double multiplier = PriorityMultipliers[priority];
        return (basePrice * multiplier) + (hoursSpent * HourlyRate);
    }
}