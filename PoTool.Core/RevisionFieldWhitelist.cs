namespace PoTool.Core;

/// <summary>
/// Single source of truth for the revision field whitelist.
/// These are the TFS work item fields stored in revision headers/deltas
/// and used for validation comparison.
/// </summary>
public static class RevisionFieldWhitelist
{
    public sealed record ODataRevisionFieldMapping(
        string? ScalarProperty,
        string? NavigationProperty,
        IReadOnlyList<string> SelectProperties,
        IReadOnlyList<string> ReadProperties);

    public sealed record CanonicalRevisionFieldSpec(
        string RestFieldRef,
        ODataRevisionFieldMapping ODataRevisionMapping,
        bool IsRequired);

    public sealed record ODataRevisionParseDescriptor(
        string RestFieldRef,
        string? ScalarProperty,
        string? NavigationProperty,
        IReadOnlyList<string> ReadProperties,
        bool IsRequired);

    public sealed record ODataRevisionSelectionSpec(
        IReadOnlyList<string> TopLevelSelect,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Expands,
        IReadOnlyList<ODataRevisionParseDescriptor> ParseDescriptors);

    /// <summary>
    /// The canonical set of fields requested from the TFS reporting API
    /// and stored in revision data. Used by both ingestion and validation.
    /// </summary>
    public static readonly IReadOnlyList<CanonicalRevisionFieldSpec> CanonicalFields = new[]
    {
        Scalar("System.Id", "WorkItemId", isRequired: true),
        Scalar("System.WorkItemType", "WorkItemType"),
        Scalar("System.Title", "Title"),
        Scalar("System.State", "State"),
        Scalar("System.Reason", "Reason"),
        Navigation("System.IterationPath", "Iteration", ["IterationPath"], readProperties: ["IterationPath"]),
        Navigation("System.AreaPath", "Area", ["AreaPath"], readProperties: ["AreaPath"]),
        Scalar("System.CreatedDate", "CreatedDate"),
        Scalar("System.ChangedDate", "ChangedDate"),
        Navigation("System.ChangedBy", "ChangedBy", ["UserName", "UserEmail", "UserId"], readProperties: ["UserName", "UserEmail", "UserId"]),
        Scalar("Microsoft.VSTS.Common.ClosedDate", "ClosedDate"),
        Scalar("Microsoft.VSTS.Scheduling.Effort", "Effort"),
        Scalar("Microsoft.VSTS.Scheduling.StoryPoints", "StoryPoints"),
        Scalar("Microsoft.VSTS.Common.BusinessValue", "BusinessValue"),
        Scalar("Microsoft.VSTS.Common.TimeCriticality", "TimeCriticality"),
        Scalar("Rhodium.Funding.ProjectNumber", "ProjectNumber"),
        Scalar("Rhodium.Funding.ProjectElement", "ProjectElement"),
        Scalar("System.Tags", "TagNames"),
        Scalar("Microsoft.VSTS.Common.Severity", "Severity")
    };

    public static readonly IReadOnlyList<string> RestFields = CanonicalFields.Select(field => field.RestFieldRef).ToArray();

    public static readonly IReadOnlyList<string> Fields = RestFields;

    public static ODataRevisionSelectionSpec BuildODataRevisionSelectionSpec(bool includeRevision = true)
    {
        var topLevelSelect = new List<string>();
        var expands = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var parseDescriptors = new List<ODataRevisionParseDescriptor>(CanonicalFields.Count);

        foreach (var field in CanonicalFields)
        {
            var mapping = field.ODataRevisionMapping;
            if (!string.IsNullOrWhiteSpace(mapping.ScalarProperty))
            {
                AddUnique(topLevelSelect, mapping.ScalarProperty!);
            }
            else if (!string.IsNullOrWhiteSpace(mapping.NavigationProperty))
            {
                if (!expands.TryGetValue(mapping.NavigationProperty!, out var selectProperties))
                {
                    selectProperties = new List<string>();
                    expands[mapping.NavigationProperty!] = selectProperties;
                }

                foreach (var selectProperty in mapping.SelectProperties)
                {
                    AddUnique(selectProperties, selectProperty);
                }
            }

            parseDescriptors.Add(new ODataRevisionParseDescriptor(
                field.RestFieldRef,
                mapping.ScalarProperty,
                mapping.NavigationProperty,
                mapping.ReadProperties,
                field.IsRequired));
        }

        if (includeRevision)
        {
            if (topLevelSelect.Count == 0)
            {
                topLevelSelect.Add("Revision");
            }
            else
            {
                topLevelSelect.Insert(1, "Revision");
            }
        }

        return new ODataRevisionSelectionSpec(
            topLevelSelect,
            expands.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal),
            parseDescriptors);
    }

    private static CanonicalRevisionFieldSpec Scalar(string restFieldRef, string scalarProperty, bool isRequired = false)
        => new(
            restFieldRef,
            new ODataRevisionFieldMapping(
                ScalarProperty: scalarProperty,
                NavigationProperty: null,
                SelectProperties: [],
                ReadProperties: [scalarProperty]),
            isRequired);

    private static CanonicalRevisionFieldSpec Navigation(
        string restFieldRef,
        string navigationProperty,
        IReadOnlyList<string> selectProperties,
        IReadOnlyList<string> readProperties,
        bool isRequired = false)
        => new(
            restFieldRef,
            new ODataRevisionFieldMapping(
                ScalarProperty: null,
                NavigationProperty: navigationProperty,
                SelectProperties: selectProperties,
                ReadProperties: readProperties),
            isRequired);

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.Ordinal))
        {
            values.Add(value);
        }
    }
}
