using MatchAnalystPro.Server.Data;
using MatchAnalystPro.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace MatchAnalystPro.Server.Services;

public record DiscoveredVideo(string Name, string FullPath, double SizeMb, DateTime LastModified);

public class VideoService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public VideoService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string GetVideosFolder()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatchAnalystPro", "videos");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string GetWebRootVideosFolder()
    {
        var webRoot = !string.IsNullOrEmpty(_env.WebRootPath)
            ? _env.WebRootPath
            : Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var folder = Path.Combine(webRoot, "videos");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// إرجاع رابط الويب الصحيح لتشغيل الفيديو عبر المتصفح مع ضمان وجود نسخة أو رابط في wwwroot/videos كبديل مضمون يدعم التقديم والترجيع (HTTP Range)
    /// </summary>
    public string ResolvePlayableVideoUrl(Match match)
    {
        if (string.IsNullOrWhiteSpace(match.VideoPath) || !File.Exists(match.VideoPath))
            return "";

        try
        {
            var ext = Path.GetExtension(match.VideoPath);
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";
            var webFileName = $"match_{match.Id}{ext}";
            var webFilePath = Path.Combine(GetWebRootVideosFolder(), webFileName);

            var srcInfo = new FileInfo(match.VideoPath);
            if (!File.Exists(webFilePath) || new FileInfo(webFilePath).Length != srcInfo.Length)
            {
                // إذا كان حجم الملف أقل من 600 ميجابايت ننسخه إلى wwwroot/videos لضمان التشغيل الفوري عبر StaticFiles
                if (srcInfo.Length <= 600L * 1024 * 1024)
                {
                    File.Copy(match.VideoPath, webFilePath, overwrite: true);
                }
            }

            if (File.Exists(webFilePath))
            {
                return $"/videos/{webFileName}?v={srcInfo.LastWriteTimeUtc.Ticks}";
            }
        }
        catch { }

        return $"/api/video/{match.Id}/0?v={DateTime.UtcNow.Ticks}";
    }

    public async Task<(bool success, string message, string path)> SaveUploadedVideoAsync(
        int matchId, 
        string fileName, 
        Stream inputStream, 
        long totalBytes,
        Func<int, Task>? onProgress = null)
    {
        try
        {
            var match = await _db.Matches.FindAsync(matchId);
            if (match == null) return (false, "المباراة غير موجودة", "");

            var cleanName = Path.GetFileName(fileName);
            var ext = Path.GetExtension(cleanName);
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";

            // نحفظ الفيديو مباشرة داخل wwwroot/videos لضمان عمله فوراً عبر StaticFiles مع دعم التقديم والترجيع بالفريم
            var destPath = Path.Combine(GetWebRootVideosFolder(), $"match_{matchId}{ext}");

            const int bufferSize = 512 * 1024; // 512 KB buffer
            var buffer = new byte[bufferSize];
            long totalRead = 0;
            int bytesRead;

            await using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
            {
                int lastReportedPercent = -1;
                while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (totalBytes > 0 && onProgress != null)
                    {
                        int percent = (int)((totalRead * 100) / totalBytes);
                        if (percent != lastReportedPercent)
                        {
                            lastReportedPercent = percent;
                            await onProgress(percent);
                        }
                    }
                }
            }

            match.VideoPath = destPath;
            await _db.SaveChangesAsync();

            return (true, "تم رفع الفيديو وربطه بالمباراة بنجاح", destPath);
        }
        catch (Exception ex)
        {
            return (false, $"فشل حفظ ملف الفيديو: {ex.Message}", "");
        }
    }

    public async Task<(bool success, string message)> LinkVideoPathAsync(int matchId, string fullPath)
    {
        var cleanPath = fullPath.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(cleanPath))
        {
            return (false, "يرجى إدخال مسار صحيح للملف");
        }

        if (!File.Exists(cleanPath))
        {
            return (false, $"الملف غير موجود في المسار المحدد: {cleanPath}");
        }

        var match = await _db.Matches.FindAsync(matchId);
        if (match == null) return (false, "المباراة غير موجودة");

        match.VideoPath = cleanPath;
        await _db.SaveChangesAsync();

        // تجهيز نسخة الويب إن أمكن
        ResolvePlayableVideoUrl(match);

        return (true, $"تم ربط الفيديو بنجاح ({Path.GetFileName(cleanPath)})");
    }

    public async Task<(bool success, string message)> UnlinkVideoAsync(int matchId)
    {
        var match = await _db.Matches.FindAsync(matchId);
        if (match == null) return (false, "المباراة غير موجودة");

        match.VideoPath = null;
        await _db.SaveChangesAsync();
        return (true, "تم فك ارتباط الفيديو بالمباراة");
    }

    public List<DiscoveredVideo> DiscoverLocalVideos()
    {
        var list = new List<DiscoveredVideo>();
        var searchFolders = new List<string>
        {
            GetWebRootVideosFolder(),
            GetVideosFolder(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            _env.WebRootPath != null ? Path.Combine(_env.WebRootPath, "clips") : ""
        };

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mkv", ".mov", ".m4v" };

        foreach (var folder in searchFolders.Distinct())
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;
                var dirInfo = new DirectoryInfo(folder);
                var files = dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => extensions.Contains(f.Extension))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(15);

                foreach (var file in files)
                {
                    if (list.Any(x => x.FullPath.Equals(file.FullName, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    list.Add(new DiscoveredVideo(
                        file.Name,
                        file.FullName,
                        Math.Round(file.Length / (1024.0 * 1024.0), 2),
                        file.LastWriteTime
                    ));
                }
            }
            catch { }
        }

        return list;
    }

    public async Task<(bool success, string message, string path)> CreateOrDownloadDemoVideoAsync(int matchId)
    {
        try
        {
            var match = await _db.Matches.FindAsync(matchId);
            if (match == null) return (false, "المباراة غير موجودة", "");

            var demoPath = Path.Combine(GetWebRootVideosFolder(), $"match_{matchId}.mp4");
            var existingSample = Path.Combine(GetVideosFolder(), "sample_match.mp4");

            if (File.Exists(existingSample))
            {
                File.Copy(existingSample, demoPath, overwrite: true);
            }
            else if (!File.Exists(demoPath))
            {
                // 1) محاولة توليد فيديو ملعب تدريبي محلياً عبر FFmpeg (يعمل بدون إنترنت 100%)
                await TryGenerateSyntheticPitchVideoAsync(demoPath);

                // 2) إذا لم يتوفر FFmpeg، نحاول تنزيل عينة الفيديو القياسية
                if (!File.Exists(demoPath))
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    try
                    {
                        var bytes = await httpClient.GetByteArrayAsync("https://www.w3schools.com/html/mov_bbb.mp4");
                        if (bytes.Length > 1000)
                        {
                            await File.WriteAllBytesAsync(demoPath, bytes);
                        }
                    }
                    catch { }
                }
            }

            if (!File.Exists(demoPath))
            {
                return (false, "تعذر تجهيز العينة التجريبية تلقائياً، يرجى استخدام زر (فتح فيديو فوري من جهازك) أو رفع ملف MP4", "");
            }

            match.VideoPath = demoPath;
            match.VideoFps = 25;
            await _db.SaveChangesAsync();

            return (true, "تم تجهيز الفيديو التجريبي بنجاح وربطه بالمباراة", demoPath);
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ أثناء إعداد الفيديو التجريبي: {ex.Message}", "");
        }
    }

    private static async Task TryGenerateSyntheticPitchVideoAsync(string outputPath)
    {
        try
        {
            var ffmpeg = ClipService.GetResolvedFfmpegPath();
            if (string.IsNullOrEmpty(ffmpeg)) return;

            var args = $"-y -f lavfi -i \"testsrc2=size=1280x720:rate=25:duration=30\" -c:v libx264 -pix_fmt yuv420p -preset ultrafast \"{outputPath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var errTask = proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();
                _ = await errTask;
            }
        }
        catch { }
    }
}
