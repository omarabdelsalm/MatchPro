using MatchAnalystPro.Server.Data;
using MatchAnalystPro.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace MatchAnalystPro.Server.Services;

public class ClipService
{
    private static readonly Lazy<string?> _cachedFfmpegPath = new(FindFfmpeg);

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly string _ffmpegPath;
    private readonly string _outputDir;

    public ClipService(AppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        
        var configuredPath = config["FFmpeg:Path"];
        if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
        {
            _ffmpegPath = configuredPath;
        }
        else
        {
            _ffmpegPath = _cachedFfmpegPath.Value ?? "ffmpeg";
        }

        var webRoot = !string.IsNullOrEmpty(env.WebRootPath)
            ? env.WebRootPath
            : Path.Combine(AppContext.BaseDirectory, "wwwroot");
        _outputDir = Path.Combine(webRoot, "clips");
        Directory.CreateDirectory(_outputDir);
    }

    public static string? GetResolvedFfmpegPath() => _cachedFfmpegPath.Value;

    public string GetClipsFolder() => _outputDir;

    /// <summary>توليد مقطع MP4 من إطار بداية إلى إطار نهاية محددين مباشرة</summary>
    public async Task<string> GenerateRangeClipAsync(int matchId, long startFrame, long endFrame)
    {
        var match = await _db.Matches.FindAsync(matchId);
        if (match?.VideoPath == null || !File.Exists(match.VideoPath))
            throw new InvalidOperationException("ملف الفيديو الأصلي غير مرتبط على القرص الصلب لهذه المباراة");

        var fps = match.VideoFps > 0 ? match.VideoFps : 25;
        var startTime = Math.Max(0, startFrame) / (double)fps;
        var endTime = Math.Max(startFrame + fps, endFrame) / (double)fps;
        var duration = Math.Max(1.0, endTime - startTime);

        var outputPath = Path.Combine(_outputDir, $"clip_{matchId}_{startFrame}_{endFrame}_{DateTime.Now:HHmmss}.mp4");
        var startStr = startTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var durStr = duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        var args = $"-y -ss {startStr} -i \"{match.VideoPath}\" " +
                   $"-t {durStr} -c:v libx264 -preset fast -crf 23 " +
                   $"-c:a aac -b:a 128k \"{outputPath}\"";

        await RunFFmpegAsync(args);
        return outputPath;
    }

    /// <summary>توليد مقطع MP4 لحدث واحد</summary>
    public async Task<string> GenerateEventClipAsync(long eventId, int paddingSeconds = 5)
    {
        var ev = await _db.Events.Include(e => e.Match)
            .FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev?.Match?.VideoPath == null || !File.Exists(ev.Match.VideoPath))
            throw new InvalidOperationException("حدث أو ملف فيديو غير موجود");

        var fps = ev.Match.VideoFps > 0 ? ev.Match.VideoFps : 25;
        var startFrame = ev.ClipStartFrame > 0 && ev.ClipEndFrame > ev.ClipStartFrame
            ? ev.ClipStartFrame
            : Math.Max(0, ev.FrameNumber - paddingSeconds * fps);
        var endFrame = ev.ClipEndFrame > ev.ClipStartFrame
            ? ev.ClipEndFrame
            : (ev.FrameNumber + paddingSeconds * fps);

        var startTime = startFrame / (double)fps;
        var endTime = endFrame / (double)fps;
        var duration = Math.Max(1.0, endTime - startTime);

        var outputPath = Path.Combine(_outputDir, $"event_{eventId}_{DateTime.Now:yyyyMMddHHmmss}.mp4");

        var startStr = startTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var durStr = duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        var args = $"-y -ss {startStr} -i \"{ev.Match.VideoPath}\" " +
                   $"-t {durStr} -c:v libx264 -preset fast -crf 23 " +
                   $"-c:a aac -b:a 128k \"{outputPath}\"";

        await RunFFmpegAsync(args);
        return outputPath;
    }

    /// <summary>توليد مقطع مركب لعدة أحداث متتالية</summary>
    public async Task<string> GenerateMultiEventClipAsync(
        IEnumerable<long> eventIds, int paddingSeconds = 3)
    {
        var idsList = eventIds.ToList();
        var events = await _db.Events.Include(e => e.Match)
            .Where(e => idsList.Contains(e.Id))
            .OrderBy(e => e.FrameNumber)
            .ToListAsync();
        
        if (events.Count == 0) throw new InvalidOperationException("لا توجد أحداث محددة لتصديرها");

        var match = events[0].Match!;
        if (string.IsNullOrEmpty(match.VideoPath) || !File.Exists(match.VideoPath))
            throw new InvalidOperationException("ملف الفيديو غير موجود على القرص الصلب");

        var fps = match.VideoFps > 0 ? match.VideoFps : 25;

        var clipFiles = new List<string>();
        foreach (var ev in events)
        {
            var startFrame = ev.ClipStartFrame > 0 && ev.ClipEndFrame > ev.ClipStartFrame
                ? ev.ClipStartFrame
                : Math.Max(0, ev.FrameNumber - paddingSeconds * fps);
            var endFrame = ev.ClipEndFrame > ev.ClipStartFrame
                ? ev.ClipEndFrame
                : (ev.FrameNumber + paddingSeconds * fps);
            var startTime = startFrame / (double)fps;
            var duration = Math.Max(1.0, (endFrame - startFrame) / (double)fps);

            var partPath = Path.Combine(_outputDir, $"part_{ev.Id}_{Guid.NewGuid():N}.mp4");

            var startStr = startTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            var durStr = duration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

            var args = $"-y -ss {startStr} -i \"{match.VideoPath}\" " +
                       $"-t {durStr} -c:v libx264 -preset fast -crf 23 " +
                       $"-c:a aac -b:a 128k \"{partPath}\"";
            await RunFFmpegAsync(args);
            clipFiles.Add(partPath);
        }

        // دمج المقاطع
        var listFile = Path.Combine(_outputDir, $"list_{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(listFile, 
            clipFiles.Select(f => $"file '{f.Replace("\\", "/")}'"));

        var outputPath = Path.Combine(_outputDir, 
            $"montage_{DateTime.Now:yyyyMMddHHmmss}.mp4");
        
        await RunFFmpegAsync(
            $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"");

        // تنظيف الملفات المؤقتة
        foreach (var f in clipFiles) { try { File.Delete(f); } catch { } }
        try { File.Delete(listFile); } catch { }

        return outputPath;
    }

    /// <summary>استخراج صورة Freeze Frame من إطار محدد</summary>
    public async Task<string> ExtractFrameAsync(int matchId, long frameNumber)
    {
        var match = await _db.Matches.FindAsync(matchId);
        if (match?.VideoPath == null || !File.Exists(match.VideoPath))
            throw new InvalidOperationException("ملف الفيديو غير موجود");

        var fps = match.VideoFps > 0 ? match.VideoFps : 25;
        var time = frameNumber / (double)fps;
        var outputPath = Path.Combine(_outputDir, 
            $"frame_{matchId}_{frameNumber}_{Guid.NewGuid():N}.jpg");

        var timeStr = time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        await RunFFmpegAsync(
            $"-y -ss {timeStr} -i \"{match.VideoPath}\" -frames:v 1 -q:v 2 \"{outputPath}\"");

        return outputPath;
    }

    private async Task RunFFmpegAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) throw new Exception("تعذر بدء تشغيل FFmpeg");

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            _ = await stdOutTask;
            var err = await stdErrTask;

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg فشل: {err}");
            }
        }
        catch (Exception ex) when (!File.Exists(_ffmpegPath))
        {
            throw new Exception($"برنامج FFmpeg غير مثبت في المسار ({_ffmpegPath}). يرجى تنزيل FFmpeg أو تثبيته عبر (winget install Gyan.FFmpeg). التفاصيل: {ex.Message}");
        }
    }

    private static string? FindFfmpeg()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Links\ffmpeg.exe"),
            @"C:\ffmpeg\bin\ffmpeg.exe"
        };

        try
        {
            var wingetPkg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Packages");
            if (Directory.Exists(wingetPkg))
            {
                var found = Directory.GetFiles(wingetPkg, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(found)) candidates.Add(found);
            }
        }
        catch { }

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        var processPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var combined = $"{processPath};{userPath};{machinePath}";

        foreach (var p in combined.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(p.Trim().Trim('"'), "ffmpeg.exe");
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        return null;
    }
}
