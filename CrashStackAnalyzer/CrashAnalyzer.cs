using System.Text.Json;
using CrashStackAnalyzer.LanguageDetection;
using CrashStackAnalyzer.Models;
using CrashStackAnalyzer.Normalization;
using CrashStackAnalyzer.Parsers;
using CrashStackAnalyzer.Parsers.Language;

namespace CrashStackAnalyzer;

/// <summary>
/// 崩溃堆栈分析器主类
/// </summary>
public class CrashAnalyzer
{
    private readonly LanguageDetector _languageDetector;
    private readonly StackTraceNormalizer _normalizer;
    private readonly Dictionary<LanguageType, IStackParser> _parsers;

    public CrashAnalyzer()
    {
        _languageDetector = new LanguageDetector();
        _normalizer = new StackTraceNormalizer();

        _parsers = new Dictionary<LanguageType, IStackParser>
        {
            [LanguageType.CSharp] = new CSharpStackParser(),
            [LanguageType.Java] = new JavaStackParser()
        };
    }

    /// <summary>
    /// 注册自定义解析器
    /// </summary>
    public void RegisterParser(IStackParser parser)
    {
        _parsers[parser.SupportedLanguage] = parser;
    }

    /// <summary>
    /// 分析堆栈文本
    /// </summary>
    public CrashReport Analyze(string rawStackTrace)
    {
        var report = new CrashReport
        {
            RawStackTrace = rawStackTrace,
            ParsedAt = DateTime.UtcNow
        };

        try
        {
            // 1. 标准化文本
            var normalizedText = _normalizer.Normalize(rawStackTrace);

            // 2. 检测语言
            report.PrimaryLanguage = _languageDetector.Detect(normalizedText);

            // 3. 使用对应的解析器
            if (_parsers.TryGetValue(report.PrimaryLanguage, out var parser))
            {
                report.Frames = parser.ParseFrames(normalizedText);
                report.MainException = parser.ExtractException(normalizedText);
                report.Metadata = parser.ExtractMetadata(normalizedText);
            }
            else
            {
                // 无法识别的语言，尝试所有解析器
                foreach (var kvp in _parsers)
                {
                    if (kvp.Value.CanParse(normalizedText))
                    {
                        report.PrimaryLanguage = kvp.Key;
                        report.Frames = kvp.Value.ParseFrames(normalizedText);
                        report.MainException = kvp.Value.ExtractException(normalizedText);
                        report.Metadata = kvp.Value.ExtractMetadata(normalizedText);
                        break;
                    }
                }
            }

            // 4. 添加通用元数据
            AddGeneralMetadata(report, normalizedText);
        }
        catch (Exception ex)
        {
            report.Metadata["ParseError"] = ex.Message;
            report.Metadata["ParseErrorDetails"] = ex.ToString();
        }

        return report;
    }

    /// <summary>
    /// 批量分析多个堆栈
    /// </summary>
    public async Task<List<CrashReport>> AnalyzeBatchAsync(
        IEnumerable<string> stackTraces,
        IProgress<int>? progress = null)
    {
        var reports = new List<CrashReport>();
        var traces = stackTraces.ToList();
        
        for (int i = 0; i < traces.Count; i++)
        {
            var report = await Task.Run(() => Analyze(traces[i]));
            reports.Add(report);
            progress?.Report((i + 1) * 100 / traces.Count);
        }

        return reports;
    }

    private void AddGeneralMetadata(CrashReport report, string normalizedText)
    {
        // 统计信息
        report.Metadata["CharacterCount"] = normalizedText.Length;
        report.Metadata["WordCount"] = normalizedText.Split(' ', '\n').Length;
        
        // 检查是否有文件信息
        report.Metadata["HasFileInfo"] = report.Frames.Any(f => f.FilePath != null);
        report.Metadata["HasLineNumbers"] = report.Frames.Any(f => f.LineNumber.HasValue);
        
        // 技术栈检测
        var techStack = DetectTechnologyStack(normalizedText);
        if (techStack.Any())
        {
            report.Metadata["TechnologyStack"] = techStack;
        }
    }

    private List<string> DetectTechnologyStack(string text)
    {
        var technologies = new List<string>();

        var techPatterns = new Dictionary<string, string>
        {
            ["ASP.NET"] = @"\b(System\.Web\.|Microsoft\.AspNetCore\.)",
            ["Entity Framework"] = @"\b(System\.Data\.Entity\.|Microsoft\.EntityFrameworkCore\.)",
            ["Spring"] = @"\b(org\.springframework\.)",
            ["Hibernate"] = @"\b(org\.hibernate\.)",
            ["Log4j"] = @"\b(org\.apache\.log4j\.)",
            ["Android"] = @"\b(android\.|com\.android\.)",
            ["JUnit"] = @"\b(org\.junit\.|junit\.framework\.)"
        };

        foreach (var pattern in techPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern.Value))
            {
                technologies.Add(pattern.Key);
            }
        }

        return technologies;
    }

    /// <summary>
    /// 导出为 JSON 格式
    /// </summary>
    public string ExportToJson(CrashReport report, bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(report, options);
    }

    /// <summary>
    /// 导出为文本格式
    /// </summary>
    public string ExportToText(CrashReport report)
    {
        var writer = new StringWriter();
        
        writer.WriteLine("=== Crash Report Analysis ===");
        writer.WriteLine($"Language: {report.PrimaryLanguage}");
        writer.WriteLine($"Parsed at: {report.ParsedAt:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine();
        
        writer.WriteLine("--- Exception ---");
        writer.WriteLine($"Type: {report.MainException.ExceptionType}");
        writer.WriteLine($"Message: {report.MainException.Message}");
        
        if (report.MainException.InnerException != null)
        {
            writer.WriteLine("Inner Exception:");
            WriteInnerException(writer, report.MainException.InnerException, 1);
        }
        
        writer.WriteLine();
        writer.WriteLine($"--- Stack Trace ({report.Frames.Count} frames) ---");
        
        for (int i = 0; i < report.Frames.Count; i++)
        {
            var frame = report.Frames[i];
            writer.WriteLine($"{i + 1,3}. {frame}");
        }
        
        writer.WriteLine();
        writer.WriteLine("--- Metadata ---");
        foreach (var kvp in report.Metadata.OrderBy(k => k.Key))
        {
            writer.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        return writer.ToString();
    }

    private void WriteInnerException(TextWriter writer, ExceptionInfo exception, int level)
    {
        var indent = new string(' ', level * 2);
        writer.WriteLine($"{indent}--> Type: {exception.ExceptionType}");
        writer.WriteLine($"{indent}    Message: {exception.Message}");
        
        if (exception.InnerException != null)
        {
            WriteInnerException(writer, exception.InnerException, level + 1);
        }
    }
}