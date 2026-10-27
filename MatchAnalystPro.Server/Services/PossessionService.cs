using MatchAnalystPro.Server.Data;
using MatchAnalystPro.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchAnalystPro.Server.Services;

public class PossessionService
{
    private readonly AppDbContext _db;
    public PossessionService(AppDbContext db) => _db = db;

    public async Task<List<Possession>> BuildPossessionsAsync(int matchId)
    {
        var events = await _db.Events
            .Where(e => e.MatchId == matchId)
            .OrderBy(e => e.Period)
            .ThenBy(e => e.FrameNumber)
            .ThenBy(e => e.Id)
            .ToListAsync();

        // حذف الحيازات القديمة
        var old = await _db.Possessions.Where(p => p.MatchId == matchId).ToListAsync();
        if (old.Count > 0)
        {
            _db.Possessions.RemoveRange(old);
            await _db.SaveChangesAsync();
        }

        var possessions = new List<Possession>();
        if (events.Count == 0) return possessions;

        int seq = 1;
        var first = events[0];
        var current = new Possession
        {
            MatchId = matchId,
            TeamId = first.TeamId,
            SequenceNumber = seq,
            Period = first.Period,
            StartFrame = first.FrameNumber,
            StartMs = first.TimestampMs,
            StartType = first.Category,
            EventCount = 0
        };

        var currentEvents = new List<Event>();
        var possessionEventMap = new List<(Possession Poss, List<Event> Evts)>();

        foreach (var e in events)
        {
            bool changeTeam = e.TeamId != current.TeamId || e.Period != current.Period;
            bool breaking = e.Category is "shot" or "foul" or "set_piece" or "goalkeeper"
                            || e.SubType == "clearance";

            if (changeTeam)
            {
                if (currentEvents.Count > 0)
                {
                    FinalizePossession(current, currentEvents, e.Category);
                    possessions.Add(current);
                    possessionEventMap.Add((current, new List<Event>(currentEvents)));
                }

                seq++;
                currentEvents.Clear();
                current = new Possession
                {
                    MatchId = matchId,
                    TeamId = e.TeamId,
                    SequenceNumber = seq,
                    Period = e.Period,
                    StartFrame = e.FrameNumber,
                    StartMs = e.TimestampMs,
                    StartType = e.Category
                };
            }

            currentEvents.Add(e);
            current.EventCount = currentEvents.Count;
            current.EndFrame = e.FrameNumber;
            current.EndMs = e.TimestampMs;

            // إذا كان الحدث ينهي الحيازة الحالية (مثل تسديدة أو خطأ أو تشتيت)
            if (breaking)
            {
                FinalizePossession(current, currentEvents, e.SubType == "clearance" ? "clearance" : e.Category);
                possessions.Add(current);
                possessionEventMap.Add((current, new List<Event>(currentEvents)));

                seq++;
                currentEvents.Clear();
                current = new Possession
                {
                    MatchId = matchId,
                    TeamId = e.TeamId,
                    SequenceNumber = seq,
                    Period = e.Period,
                    StartFrame = e.FrameNumber,
                    StartMs = e.TimestampMs,
                    StartType = "open_play"
                };
            }
        }

        if (currentEvents.Count > 0)
        {
            FinalizePossession(current, currentEvents, currentEvents.Last().Category);
            possessions.Add(current);
            possessionEventMap.Add((current, new List<Event>(currentEvents)));
        }

        _db.Possessions.AddRange(possessions);
        await _db.SaveChangesAsync();

        // ربط الأحداث بالحيازات في الذاكرة مباشرة بدلاً من استعلام N+1 داخل حلقة
        foreach (var (poss, evts) in possessionEventMap)
        {
            foreach (var ev in evts)
            {
                ev.PossessionId = poss.Id;
            }
        }
        await _db.SaveChangesAsync();

        return possessions;
    }

    private static void FinalizePossession(Possession p, List<Event> evts, string endType)
    {
        var last = evts.Last();
        p.EndFrame = last.FrameNumber;
        p.EndMs = last.TimestampMs;
        p.EndType = endType;
        p.EventCount = evts.Count;
        p.LedToShot = evts.Any(x => x.Category == "shot");
        p.LedToGoal = evts.Any(x => x.Category == "shot" && (x.Outcome == "goal" || (x.QualifiersJson != null && x.QualifiersJson.Contains("goal"))));
    }
}
