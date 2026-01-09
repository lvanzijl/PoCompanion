namespace PoTool.Shared.WorkItems;

/// <summary>
/// Immutable DTO for work items.
/// 
/// PERFORMANCE NOTE (Phase 4.1 - Future Optimization):
/// JsonPayload stores the complete TFS work item JSON response, which can be large
/// (typically 2-10 KB per item, up to 50+ KB for items with extensive history/relations).
/// 
/// OPTIMIZATION OPPORTUNITIES:
/// 1. Make JsonPayload optional via configuration flag (StoreRawPayload: bool)
/// 2. Store only selected fields instead of full payload (reduce by 70-90%)
/// 3. Apply compression (gzip/deflate) to stored payloads (reduce by 60-80%)
/// 4. Implement lazy-loading pattern: Store ID only, fetch on demand
/// 
/// TRADEOFFS:
/// - With JsonPayload: Full audit trail, debugging, custom field access
/// - Without JsonPayload: Lower memory (important for large backlogs 1000+ items)
/// 
/// CURRENT USAGE: JsonPayload is used for:
/// - Audit logging and debugging (helpful but not critical)
/// - Custom field extraction (rare, could be fetched on-demand)
/// - Not used in normal application flow (all needed data is in typed properties)
/// </summary>
public sealed record WorkItemDto(
    int TfsId,
    string Type,
    string Title,
    int? ParentTfsId,
    string AreaPath,
    string IterationPath,
    string State,
    string JsonPayload,  // Consider making this optional in future (Phase 4.1)
    DateTimeOffset RetrievedAt,
    int? Effort
);
