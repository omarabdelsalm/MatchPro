using MatchAnalystPro.Server.Models;
using MatchAnalystPro.Server.Taxonomy;
using System.Text;

namespace MatchAnalystPro.Server.Services.Export;

public class WyscoutExporter
{
    public string Export(Match match, List<Event> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MatchId,TeamId,PlayerId,EventType,EventSubType,Outcome,X,Y,EndX,EndY,Minute,Second");

        foreach (var e in events.OrderBy(x => x.FrameNumber))
        {
            var sub = EventTaxonomy.GetSubType(e.Category, e.SubType);
            sb.AppendLine(string.Join(",",
                match.Id,
                e.TeamId,
                e.PlayerId ?? 0,
                MapWyscoutType(e.Category),
                EscapeCsv(sub?.Ar ?? e.SubType),
                e.Outcome == "successful" ? "1" : "0",
                e.X?.ToString("F2") ?? "",
                e.Y?.ToString("F2") ?? "",
                e.EndX?.ToString("F2") ?? "",
                e.EndY?.ToString("F2") ?? "",
                e.Minute,
                e.Second
            ));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

    private static int MapWyscoutType(string cat) => cat switch
    {
        "pass" => 8,
        "shot" => 10,
        "carry" => 9,
        "duel" => 1,
        "interception" => 2,
        "foul" => 4,
        _ => 0
    };
}
