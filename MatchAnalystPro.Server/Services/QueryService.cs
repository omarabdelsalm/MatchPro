using MatchAnalystPro.Server.Data;
using MatchAnalystPro.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchAnalystPro.Server.Services;

public class QueryFilter
{
    public int MatchId { get; set; }
    public string? Category { get; set; }
    public string? SubType { get; set; }
    public string? Outcome { get; set; }
    public int TeamId { get; set; } = 0;
    public int PlayerId { get; set; } = 0;
    public double? MinX { get; set; }
    public double? MaxX { get; set; }
    public double? MinY { get; set; }
    public double? MaxY { get; set; }
    public int? MinMinute { get; set; }
    public int? MaxMinute { get; set; }
    public List<string> Qualifiers { get; set; } = new();
    public bool OnlyLeadingToShot { get; set; }
    public bool OnlyLeadingToGoal { get; set; }
}

public class EventFilterCriteria
{
    public int MatchId { get; set; }
    public int? TeamId { get; set; }
    public int? PlayerId { get; set; }
    public string? Category { get; set; }
    public string? SubType { get; set; }
    public string? Outcome { get; set; }
    public int? Period { get; set; }
    public int? FromMinute { get; set; }
    public int? ToMinute { get; set; }
    public string? Qualifier { get; set; }
    public bool? OnlyWithCoordinates { get; set; }
    public string? SearchTerm { get; set; }
}

public class QuerySummaryStats
{
    public int TotalEvents { get; set; }
    public int SuccessfulEvents { get; set; }
    public double SuccessRate => TotalEvents > 0 ? (double)SuccessfulEvents / TotalEvents * 100 : 0;
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    public Dictionary<string, int> PlayerCounts { get; set; } = new();
}

public class QueryService
{
    private readonly AppDbContext _db;
    public QueryService(AppDbContext db) => _db = db;

    public async Task<List<Event>> ExecuteAsync(QueryFilter f)
    {
        var q = _db.Events
            .Include(e => e.Player)
            .Include(e => e.Possession)
            .Where(e => e.MatchId == f.MatchId);

        if (!string.IsNullOrEmpty(f.Category)) q = q.Where(e => e.Category == f.Category);
        if (!string.IsNullOrEmpty(f.SubType)) q = q.Where(e => e.SubType == f.SubType);
        if (!string.IsNullOrEmpty(f.Outcome)) q = q.Where(e => e.Outcome == f.Outcome);
        if (f.TeamId > 0) q = q.Where(e => e.TeamId == f.TeamId);
        if (f.PlayerId > 0) q = q.Where(e => e.PlayerId == f.PlayerId);
        
        if (f.MinX.HasValue) q = q.Where(e => e.X >= f.MinX.Value);
        if (f.MaxX.HasValue) q = q.Where(e => e.X <= f.MaxX.Value);
        if (f.MinY.HasValue) q = q.Where(e => e.Y >= f.MinY.Value);
        if (f.MaxY.HasValue) q = q.Where(e => e.Y <= f.MaxY.Value);
        
        if (f.MinMinute.HasValue) q = q.Where(e => e.Minute >= f.MinMinute.Value);
        if (f.MaxMinute.HasValue) q = q.Where(e => e.Minute <= f.MaxMinute.Value);
        
        if (f.OnlyLeadingToShot)
            q = q.Where(e => e.Possession != null && e.Possession.LedToShot);
        if (f.OnlyLeadingToGoal)
            q = q.Where(e => e.Possession != null && e.Possession.LedToGoal);
        
        var list = await q.OrderBy(e => e.FrameNumber).ToListAsync();
        
        // فلترة Qualifiers (client-side لأنها JSON)
        if (f.Qualifiers.Count > 0)
        {
            list = list.Where(e =>
            {
                var eQuals = Newtonsoft.Json.JsonConvert
                    .DeserializeObject<List<string>>(e.QualifiersJson ?? "[]") ?? new();
                return f.Qualifiers.All(q => eQuals.Contains(q));
            }).ToList();
        }
        
        return list;
    }

    public async Task<List<Event>> QueryEventsAsync(EventFilterCriteria filter)
    {
        var q = _db.Events
            .Include(e => e.Player)
            .Include(e => e.Team)
            .Where(e => e.MatchId == filter.MatchId)
            .AsQueryable();

        if (filter.TeamId.HasValue && filter.TeamId.Value > 0)
            q = q.Where(e => e.TeamId == filter.TeamId.Value);

        if (filter.PlayerId.HasValue && filter.PlayerId.Value > 0)
            q = q.Where(e => e.PlayerId == filter.PlayerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Category))
            q = q.Where(e => e.Category == filter.Category);

        if (!string.IsNullOrWhiteSpace(filter.SubType))
            q = q.Where(e => e.SubType == filter.SubType);

        if (!string.IsNullOrWhiteSpace(filter.Outcome))
            q = q.Where(e => e.Outcome == filter.Outcome);

        if (filter.Period.HasValue && filter.Period.Value > 0)
            q = q.Where(e => e.Period == filter.Period.Value);

        if (filter.FromMinute.HasValue)
            q = q.Where(e => e.Minute >= filter.FromMinute.Value);

        if (filter.ToMinute.HasValue)
            q = q.Where(e => e.Minute <= filter.ToMinute.Value);

        if (filter.OnlyWithCoordinates == true)
            q = q.Where(e => e.X != null && e.Y != null);

        if (!string.IsNullOrWhiteSpace(filter.Qualifier))
            q = q.Where(e => e.QualifiersJson.Contains(filter.Qualifier));

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var s = filter.SearchTerm.ToLower();
            q = q.Where(e =>
                (e.Player != null && e.Player.NameAr.ToLower().Contains(s)) ||
                (e.Player != null && e.Player.Name.ToLower().Contains(s)) ||
                e.Category.ToLower().Contains(s) ||
                e.SubType.ToLower().Contains(s) ||
                (e.Note != null && e.Note.ToLower().Contains(s)));
        }

        return await q.OrderBy(e => e.FrameNumber).ToListAsync();
    }

    public QuerySummaryStats GetStats(List<Event> events)
    {
        var stats = new QuerySummaryStats
        {
            TotalEvents = events.Count,
            SuccessfulEvents = events.Count(e => e.Outcome == "successful" || e.Outcome == "key")
        };

        foreach (var g in events.GroupBy(e => e.Category))
        {
            stats.CategoryCounts[g.Key] = g.Count();
        }

        foreach (var g in events.Where(e => e.Player != null).GroupBy(e => e.Player!.NameAr))
        {
            stats.PlayerCounts[g.Key] = g.Count();
        }

        return stats;
    }
}
