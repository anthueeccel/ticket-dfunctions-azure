using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

/// <summary>
/// Thin wrapper around <see cref="Azure.Data.Tables"/> for the <c>Tickets</c> table.
/// </summary>
/// <remarks>
/// <para>
/// Persistence strategy: every ticket is an entity with <c>PartitionKey = "Ticket"</c> and
/// <c>RowKey = TicketId</c>. The <c>Ticket</c> model is mapped to a <c>TableEntity</c>
/// (the dictionary-style entity) so we don't need an <c>ITableEntity</c> class per domain type.
/// </para>
/// <para>
/// Table Storage has no <c>decimal</c> type, so <c>FinalPrice</c> is stored as a <c>double</c>
/// column, and the <c>DateTime</c> fields as ISO-8601 strings (via <see cref="DateTime.ToString()"/> and
/// <see cref="DateTime.Parse(string)"/>). The connection string honours Azure's
/// <c>UseDevelopmentStorage=true</c> marker used by Azurite.
/// </para>
/// </remarks>
public static class TableStorageService
{
    private const string TableName = "Tickets";
    private const string PartitionKey = "Ticket";

    /// <summary>Creates the table if needed and returns a client for it.</summary>
    public static TableClient GetOrCreateClient()
    {
        string connectionString = ResolveConnectionString();
        TableServiceClient service = new TableServiceClient(connectionString);
        TableClient tableClient = service.GetTableClient(TableName);
        tableClient.CreateIfNotExists();
        return tableClient;
    }

    /// <summary>Persists a brand-new ticket (initial <see cref="TicketStatus.New"/> row).</summary>
    public static async Task SaveTicket(Ticket ticket)
    {
        TableClient client = GetOrCreateClient();
        await client.AddEntityAsync(ToEntity(ticket));
    }

    /// <summary>Loads a ticket by id from the <c>Tickets</c> table.</summary>
    public static async Task<Ticket> LoadTicket(string ticketId)
    {
        TableClient client = GetOrCreateClient();
        TableEntity entity = (await client.GetEntityAsync<TableEntity>(PartitionKey, ticketId)).Value;
        return FromEntity(entity);
    }

    /// <summary>Overwrites a ticket row (used for status/final-price updates).</summary>
    public static async Task UpdateTicket(Ticket ticket)
    {
        TableClient client = GetOrCreateClient();
        await client.UpsertEntityAsync(ToEntity(ticket), TableUpdateMode.Replace);
    }

    /// <summary>
    /// Lists tickets in the <c>Tickets</c> table, optionally filtered by status and/or
    /// closure reason, with in-memory pagination.
    /// </summary>
    /// <param name="status">
    /// Optional status name (e.g. <c>"New"</c>, <c>"Closed"</c>); case-insensitive.
    /// When <c>null</c> or empty, tickets of any status are returned.
    /// </param>
    /// <param name="closureReason">
    /// Optional closure reason name (e.g. <c>"Unassigned"</c>); case-insensitive.
    /// When <c>null</c>, no closure-reason filtering is applied.
    /// </param>
    /// <param name="skip">Number of matching tickets to skip (for paging).</param>
    /// <param name="top">Maximum number of matching tickets to return.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="status"/> or <paramref name="closureReason"/> is not a valid
    /// enum name, or when <paramref name="skip"/>/<paramref name="top"/> is negative.
    /// </exception>
    public static async Task<List<Ticket>> ListTickets(
        string? status = null,
        string? closureReason = null,
        int skip = 0,
        int top = 100)
    {
        if (skip < 0)
        {
            throw new ArgumentException("'skip' must be zero or greater.", nameof(skip));
        }
        if (top < 0)
        {
            throw new ArgumentException("'top' must be zero or greater.", nameof(top));
        }

        TicketStatus? statusValue = string.IsNullOrEmpty(status)
            ? null
            : Enum.Parse<TicketStatus>(status, ignoreCase: true);

        ClosureReason? reasonValue = string.IsNullOrEmpty(closureReason)
            ? null
            : Enum.Parse<ClosureReason>(closureReason, ignoreCase: true);

        TableClient client = GetOrCreateClient();

        AsyncPageable<TableEntity> pageable = client.QueryAsync<TableEntity>(
            entity => entity.PartitionKey == PartitionKey);

        List<Ticket> tickets = new List<Ticket>();
        await foreach (TableEntity entity in pageable)
        {
            Ticket ticket = FromEntity(entity);

            if (statusValue.HasValue && ticket.Status != statusValue.Value)
            {
                continue;
            }
            if (reasonValue.HasValue && ticket.ClosureReason != reasonValue.Value)
            {
                continue;
            }
            tickets.Add(ticket);
        }

        return tickets
            .Skip(skip)
            .Take(top)
            .ToList();
    }

    // -- mapping --------------------------------------------------------------------------------

    private static string ResolveConnectionString()
    {
        string configured = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (configured == null || configured.Length == 0 || configured.Equals("UseDevelopmentStorage=true"))
        {
            // Local dev against Azurite: the SDK understands this marker and points
            // at 127.0.0.1 table/queue emulator ports.
            return "UseDevelopmentStorage=true";
        }
        return configured; // a real Azure Storage account connection string
    }

    private static TableEntity ToEntity(Ticket ticket)
    {
        TableEntity entity = new TableEntity(PartitionKey, ticket.TicketId);
        entity["Title"] = ticket.Title;
        entity["Description"] = ticket.Description;
        entity["Category"] = ticket.Category.ToString();
        entity["Priority"] = ticket.Priority.ToString();
        entity["Status"] = ticket.Status.ToString();
        entity["AssignedAnalystId"] = (ticket.AssignedAnalystId == null ? "" : ticket.AssignedAnalystId);
        entity["HoursSpent"] = (ticket.HoursSpent == null ? 0.0 : (double)ticket.HoursSpent);
        entity["FinalPrice"] = (ticket.FinalPrice == null ? 0.0 : (double)(decimal)ticket.FinalPrice);
        entity["CreatedAt"] = ticket.CreatedAt.ToString();
        entity["ClosedAt"] = (ticket.ClosedAt == null ? "" : ticket.ClosedAt.ToString());
        entity["ClosureReason"] = ticket.ClosureReason.ToString();
        return entity;
    }

    private static Ticket FromEntity(TableEntity entity)
    {
        Ticket ticket = new Ticket();
        ticket.TicketId = (string)entity["RowKey"];
        ticket.Title = (string)entity["Title"];
        ticket.Description = (string)entity["Description"];
        ticket.Category = Enum.Parse<TicketCategory>((string)entity["Category"]);
        ticket.Priority = Enum.Parse<TicketPriority>((string)entity["Priority"]);
        ticket.Status = Enum.Parse<TicketStatus>((string)entity["Status"]);

        string assigned = (string)entity["AssignedAnalystId"];
        ticket.AssignedAnalystId = (assigned.Length == 0 ? null : assigned);

        double hours = (double)entity["HoursSpent"];
        ticket.HoursSpent = (hours == 0.0 ? null : (double?)hours);

        double price = (double)entity["FinalPrice"];
        ticket.FinalPrice = (price == 0.0 ? null : (decimal?)price);

        ticket.CreatedAt = DateTime.Parse((string)entity["CreatedAt"]);

        string closed = (string)entity["ClosedAt"];
        ticket.ClosedAt = (closed.Length == 0 ? null : (DateTime?)DateTime.Parse(closed));

        // ClosureReason was added after the initial schema; default to None for rows written before it existed.
        if (entity.TryGetValue("ClosureReason", out object? reason) && reason is string reasonText && reasonText.Length > 0)
        {
            ticket.ClosureReason = Enum.Parse<ClosureReason>(reasonText);
        }
        return ticket;
    }
}