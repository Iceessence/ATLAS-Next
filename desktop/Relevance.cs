using System.Text.RegularExpressions;

namespace AtlasNext;

// Each grammar describes questions answered by the body of a specific pinned revision.
// Match the entire request so an unknown subject, extra constraint or second question
// cannot disappear during tokenization. The router and model do not supply inputs here.
internal static class Relevance
{
    private const string Archive = @"(?:(?:the|our|an) )?(?:(?:demo|demonstration) )?archive";
    private const string Exports = @"(?:the )?(?:completed )?exports";
    private const string Retention = @"(?:(?:the )?(?:archive|export) retention(?: policy| period| duration)?|(?:the )?retention (?:policy|period|duration) (?:for|of) (?:the archive|completed exports|exports))";
    private const string Receipt = @"(?:(?:the|an?|my) )?(?:export(?:ed)? (?:task )?receipt|task receipt|receipt|export)";
    private const string Snapshot = @"(?:(?:an?|the|my) )?(?:archive|snapshot)";

    public static bool Supports(string request, Hit hit)
    {
        if(string.Equals(request,hit.Record.Id,StringComparison.OrdinalIgnoreCase)) return true;
        var query=Regex.Replace(request.Trim().ToLowerInvariant(),@"\s+"," ");
        string? question=(hit.Record.Id, hit.Record.Version) switch {
            ("fact.retention",1) =>
                $@"(?:(?:(?:what|how long) is |how many days is )?{Retention}|how long (?:will|does) (?:{Archive}|{Retention}) (?:keep|retain) {Exports}|(?:how long|for how many days) are {Exports} (?:kept|retained)(?: in {Archive})?)",
            ("fact.format",1) =>
                $@"(?:(?:what is|name) (?:the )?(?:archive|export(?: receipt)?|receipt) (?:file )?(?:format|encoding)|how is {Receipt} (?:formatted|encoded)|what (?:file )?format is {Receipt}(?: in)?|(?:what is )?the (?:format|encoding) of {Receipt})",
            ("procedure.restore",1) =>
                $@"(?:(?:how (?:do (?:i|we)|can (?:i|we)|to) |(?:show me )?(?:the )?(?:steps|instructions) to )?(?:restore|recover) {Snapshot}|(?:archive|snapshot) recovery (?:steps|procedure)|how (?:is {Snapshot}|are archives) (?:restored|recovered))",
            _ => null
        };
        return question!=null && Regex.IsMatch(query,@"\A(?:please )?"+question+@"[?.!]?\z",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    }
}
