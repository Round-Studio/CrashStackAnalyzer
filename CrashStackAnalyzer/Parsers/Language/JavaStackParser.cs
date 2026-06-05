using System.Text.RegularExpressions;
using CrashStackAnalyzer.Models;

namespace CrashStackAnalyzer.Parsers.Language;

/// <summary>
/// Java 堆栈解析器
/// </summary>
public class JavaStackParser : IStackParser
{
    public LanguageType SupportedLanguage => LanguageType.Java;

    // Java 堆栈帧模式
    private static readonly Regex FrameRegex = new(
        @"^\s*at\s+" +
        @"(?<class>(?:[\w$]+\.)+[\w$]+)\." +
        @"(?<method>[\w$<>]+)" +
        @"\((?<file>[^:]*)(?::(?<line>\d+))?\)",
        RegexOptions.Compiled);

    // 异常头模式
    private static readonly Regex ExceptionRegex = new(
        @"^((?:[\w]+\.)*[\w]+(?:Exception|Error|Throwable))(?::\s*(.*))?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 异常链模式
    private static readonly Regex CausedByRegex = new(
        @"^Caused by:\s+(.+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 抑制异常模式
    private static readonly Regex SuppressedRegex = new(
        @"^Suppressed:\s+(.+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 更多帧标记
    private static readonly Regex MoreFramesRegex = new(
        @"^\s*\.\.\.\s+(\d+)\s+more",
        RegexOptions.Compiled);

    // 本地方法标记
    private static readonly Regex NativeMethodRegex = new(
        @"\(Native Method\)",
        RegexOptions.Compiled);

    // 未知来源标记
    private static readonly Regex UnknownSourceRegex = new(
        @"\(Unknown Source\)",
        RegexOptions.Compiled);

    public bool CanParse(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return false;

        return Regex.IsMatch(stackTrace, @"\bat\s+.*\.java:\d+\)") ||
               Regex.IsMatch(stackTrace, @"\b(java\.|javax\.|com\.\w+)") ||
               CausedByRegex.IsMatch(stackTrace);
    }

    public List<StackFrame> ParseFrames(string stackTrace)
    {
        var frames = new List<StackFrame>();
        var lines = stackTrace.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var skippedMore = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // 检查 "更多帧" 标记
            var moreMatch = MoreFramesRegex.Match(trimmedLine);
            if (moreMatch.Success)
            {
                skippedMore = int.Parse(moreMatch.Groups[1].Value);
                continue;
            }

            // 跳过异常链标记行
            if (CausedByRegex.IsMatch(trimmedLine) || 
                SuppressedRegex.IsMatch(trimmedLine) ||
                trimmedLine.StartsWith("... "))
            {
                continue;
            }

            var frame = ParseFrame(trimmedLine);
            frames.Add(frame);
        }

        if (skippedMore > 0)
        {
            // 添加占位帧
            for (int i = 0; i < skippedMore; i++)
            {
                frames.Add(new StackFrame
                {
                    RawText = $"... {skippedMore - i} more",
                    MethodName = "omitted",
                    Language = LanguageType.Java,
                    IsNativeCode = false
                });
            }
        }

        return frames;
    }

    private StackFrame ParseFrame(string line)
    {
        var frame = new StackFrame
        {
            RawText = line,
            Language = LanguageType.Java
        };

        var match = FrameRegex.Match(line);
        if (!match.Success)
        {
            frame.MethodName = line;
            return frame;
        }

        // 解析类名（含包名）
        var fullClassName = match.Groups["class"].Value;
        frame.ClassName = fullClassName;
        
        // 提取包名
        var lastDotIndex = fullClassName.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            frame.Namespace = fullClassName[..lastDotIndex];
            frame.ClassName = fullClassName[(lastDotIndex + 1)..];
        }

        // 解析方法名
        frame.MethodName = match.Groups["method"].Value;

        // 解析文件和行号
        var fileInfo = match.Groups["file"].Value;
        if (!string.IsNullOrEmpty(fileInfo))
        {
            if (UnknownSourceRegex.IsMatch(line))
            {
                frame.FilePath = "Unknown Source";
            }
            else if (NativeMethodRegex.IsMatch(line))
            {
                frame.IsNativeCode = true;
                frame.FilePath = "Native Method";
            }
            else
            {
                frame.FilePath = fileInfo;
                
                if (match.Groups["line"].Success && 
                    int.TryParse(match.Groups["line"].Value, out var lineNumber))
                {
                    frame.LineNumber = lineNumber;
                }
            }
        }

        // 检查是否为匿名类
        if (fullClassName.Contains('$'))
        {
            var parts = fullClassName.Split('$');
            frame.Metadata["IsAnonymousClass"] = true;
            frame.Metadata["ParentClass"] = parts[0];
        }

        return frame;
    }

    public ExceptionInfo ExtractException(string stackTrace)
    {
        var exceptionInfo = new ExceptionInfo
        {
            RawText = stackTrace
        };

        // 提取主异常
        var firstLine = stackTrace.Split('\n').FirstOrDefault()?.Trim() ?? "";
        var exceptionMatch = ExceptionRegex.Match(firstLine);

        if (exceptionMatch.Success)
        {
            exceptionInfo.ExceptionType = exceptionMatch.Groups[1].Value;
            exceptionInfo.Message = exceptionMatch.Groups[2].Value.Trim();
        }
        else
        {
            exceptionInfo.ExceptionType = firstLine.Contains(':') 
                ? firstLine[..firstLine.IndexOf(':')] 
                : firstLine;
            exceptionInfo.Message = firstLine.Contains(':') 
                ? firstLine[(firstLine.IndexOf(':') + 1)..].Trim() 
                : "";
        }

        // 提取异常链
        var causedByMatches = CausedByRegex.Matches(stackTrace);
        if (causedByMatches.Count > 0)
        {
            var currentException = exceptionInfo;
            
            foreach (Match match in causedByMatches)
            {
                var causedByText = match.Groups[1].Value;
                var causedByMatch = ExceptionRegex.Match(causedByText);
                
                var innerException = new ExceptionInfo
                {
                    RawText = causedByText,
                    ExceptionType = causedByMatch.Success 
                        ? causedByMatch.Groups[1].Value 
                        : causedByText.Split(':')[0],
                    Message = causedByMatch.Success 
                        ? causedByMatch.Groups[2].Value.Trim() 
                        : causedByText.Contains(':') 
                            ? causedByText[(causedByText.IndexOf(':') + 1)..].Trim() 
                            : ""
                };

                currentException.InnerException = innerException;
                currentException = innerException;
            }
        }

        return exceptionInfo;
    }

    public Dictionary<string, object> ExtractMetadata(string stackTrace)
    {
        var metadata = new Dictionary<string, object>();

        var lines = stackTrace.Split('\n');
        metadata["TotalLines"] = lines.Length;
        metadata["FrameCount"] = lines.Count(l => l.Trim().StartsWith("at "));
        
        // 提取异常链信息
        var causedByCount = CausedByRegex.Matches(stackTrace).Count;
        metadata["ExceptionChainLength"] = causedByCount + 1;
        metadata["HasSuppressedExceptions"] = SuppressedRegex.IsMatch(stackTrace);
        
        // 线程信息
        var threadMatch = Regex.Match(stackTrace, 
            @"^(.*?)\s+(prio=\d+|tid=\d+|nid=0x[\da-fA-F]+)", 
            RegexOptions.Multiline);
        
        if (threadMatch.Success)
        {
            metadata["ThreadInfo"] = threadMatch.Value.Trim();
            metadata["ThreadName"] = threadMatch.Groups[1].Value.Trim();
        }

        // 检查是否有本地调用
        metadata["HasNativeCalls"] = NativeMethodRegex.IsMatch(stackTrace);
        
        // 统计包名
        var packages = Regex.Matches(stackTrace, @"^at\s+([\w.]+)\.")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .GroupBy(p => p)
            .ToDictionary(g => g.Key, g => g.Count());
        
        metadata["TopPackages"] = packages.OrderByDescending(p => p.Value)
                                          .Take(5)
                                          .ToList();

        return metadata;
    }
}