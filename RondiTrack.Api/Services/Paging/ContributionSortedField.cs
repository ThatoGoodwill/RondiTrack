namespace RondiTrack.Api.Services.Paging;

public enum ContributionSortField { RecordedAt, Amount }

public static class ContributionSort
{
    public static bool TryParse(string? value, out ContributionSortField field, out bool descending)
    {
        field = ContributionSortField.RecordedAt;
        descending = true;

        if (string.IsNullOrWhiteSpace(value)) return true;

        var parts = value.Split('_');
        var fieldPart = parts[0];
        descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return Enum.TryParse(fieldPart, ignoreCase: true, out field);
    }
}