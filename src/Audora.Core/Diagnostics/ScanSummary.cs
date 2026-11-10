namespace Audora.Core.Diagnostics;

public static class ScanSummary
{
    /// <summary>One short sentence, such as "No problems found, 3 notes." or "1 problem found, 2 warnings."</summary>
    public static string Describe(ScanReport report)
    {
        var problems = report.Count(ScanLevel.Problem);
        var warnings = report.Count(ScanLevel.Warning);
        var notes = report.Count(ScanLevel.Info);

        var headline = problems > 0
            ? $"{problems} {Plural(problems, "problem", "problems")} found"
            : warnings > 0
                ? $"{warnings} {Plural(warnings, "warning", "warnings")} found"
                : "No problems found";

        var extra = new List<string>();
        if (problems > 0 && warnings > 0)
        {
            extra.Add($"{warnings} {Plural(warnings, "warning", "warnings")}");
        }

        if (notes > 0)
        {
            extra.Add($"{notes} {Plural(notes, "note", "notes")}");
        }

        return extra.Count == 0 ? headline + "." : $"{headline}, {string.Join(", ", extra)}.";
    }

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;
}
