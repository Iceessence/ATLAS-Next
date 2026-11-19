using System.Text.RegularExpressions;

namespace AtlasNext;

internal sealed record ResearchPlan(string Objective,string Fields,string[] SourceIds)
{
    internal string Request=>"Compare project claims: "+Fields;

    internal bool IsValid()=>Objective is {Length:>0 and <=256} && !string.IsNullOrWhiteSpace(Objective) &&
        Fields is {Length:>0 and <=400} && Fields.Split('|',StringSplitOptions.TrimEntries) is {Length:>=2 and <=4} keys &&
        keys.All(key=>Regex.IsMatch(key,@"\A[\p{L}\p{N}][\p{L}\p{N} _-]{0,95}\z")) &&
        SourceIds is {Length:>=2 and <=8} && SourceIds.Distinct(StringComparer.Ordinal).Count()==SourceIds.Length &&
        SourceIds.All(id=>id is {Length:68} && id.StartsWith("doc-",StringComparison.Ordinal) && id.AsSpan(4).ToString().All(Uri.IsHexDigit));
}
