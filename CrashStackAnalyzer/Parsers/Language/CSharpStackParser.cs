using System.Text.RegularExpressions;
using CrashStackAnalyzer.Models;

namespace CrashStackAnalyzer.Parsers.Language;

/// <summary>
/// C# 堆栈解析器
/// </summary>
public class CSharpStackParser : IStackParser
{
    public LanguageType SupportedLanguage => LanguageType.CSharp;

    // 主要堆栈帧模式
    private static readonly Regex FrameRegex = new(
        @"^\s*at\s+(?<method>[^)]+\))\s+(?:in\s+(?<file>.*?):line\s+(?<line>\d+))?",
        RegexOptions.Compiled);

    // 异常头模式
    private static readonly Regex ExceptionRegex = new(
        @"^(?<type>[\w.]+Exception):\s*(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 内部异常模式
    private static readonly Regex InnerExceptionRegex = new(
        @"--- End of inner exception stack trace ---",
        RegexOptions.Compiled);

    // 方法签名分解
    private static readonly Regex MethodRegex = new(
        @"^(?<class>.+)\.(?<method>[^.]+)\((?<args>.*)\)$",
        RegexOptions.Compiled);

    // 本地代码标记
    private static readonly Regex NativeCodeRegex = new(
        @"\(Native\)|\[Native to Managed Transition\]",
        RegexOptions.Compiled);

    // 异步方法模式
    private static readonly Regex AsyncMethodRegex = new(
        @"<(.*?)>d__\d+\.MoveNext\(\)",
        RegexOptions.Compiled);

    // 模块名提取
    private static readonly Regex ModuleRegex = new(
        @"^at\s+.*?(?:\[(.*?)\])",
        RegexOptions.Compiled);

    public bool CanParse(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return false;

        // 检查 C# 特征
        return Regex.IsMatch(stackTrace, @"\bat\s+.*\.(cs|csx|vb):line\s+\d+") ||
               Regex.IsMatch(stackTrace, @"\b(System\.|Microsoft\.)") ||
               FrameRegex.IsMatch(stackTrace);
    }

    public List<StackFrame> ParseFrames(string stackTrace)
    {
        var frames = new List<StackFrame>();
        var lines = stackTrace.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            var frame = ParseFrame(trimmedLine);
            frames.Add(frame);
        }

        return frames;
    }

    private StackFrame ParseFrame(string line)
    {
        var frame = new StackFrame
        {
            RawText = line,
            Language = LanguageType.CSharp
        };

        // 检查是否为本地代码
        if (NativeCodeRegex.IsMatch(line))
        {
            frame.IsNativeCode = true;
            frame.MethodName = line;
            return frame;
        }

        var match = FrameRegex.Match(line);
        if (!match.Success)
        {
            frame.MethodName = line;
            return frame;
        }

        // 解析方法名
        var methodSignature = match.Groups["method"].Value.Trim();
        frame.MethodName = methodSignature;

        // 分解方法签名
        var methodMatch = MethodRegex.Match(methodSignature);
        if (methodMatch.Success)
        {
            var fullClassName = methodMatch.Groups["class"].Value;
            frame.ClassName = fullClassName;

            // 提取命名空间
            var lastDotIndex = fullClassName.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                frame.Namespace = fullClassName[..lastDotIndex];
                frame.ClassName = fullClassName[(lastDotIndex + 1)..];
            }

            // 解析异步方法
            var asyncMatch = AsyncMethodRegex.Match(methodSignature);
            if (asyncMatch.Success)
            {
                frame.MethodName = asyncMatch.Groups[1].Value;
                frame.Metadata["IsAsync"] = true;
            }
            else
            {
                frame.MethodName = methodMatch.Groups["method"].Value;
            }

            frame.Parameters = methodMatch.Groups["args"].Value;
        }

        // 解析文件和行号
        if (match.Groups["file"].Success)
        {
            frame.FilePath = match.Groups["file"].Value.Trim();

            if (match.Groups["line"].Success &&
                int.TryParse(match.Groups["line"].Value, out var lineNumber))
            {
                frame.LineNumber = lineNumber;
            }
        }

        // 提取模块名
        var moduleMatch = ModuleRegex.Match(line);
        if (moduleMatch.Success)
        {
            frame.ModuleName = moduleMatch.Groups[1].Value;
        }

        return frame;
    }

    public ExceptionInfo ExtractException(string stackTrace)
    {
        var exceptionInfo = new ExceptionInfo
        {
            RawText = stackTrace
        };

        // 提取异常类型和消息
        var match = ExceptionRegex.Match(stackTrace);
        if (match.Success)
        {
            exceptionInfo.ExceptionType = match.Groups["type"].Value.Trim();
            exceptionInfo.Message = match.Groups["message"].Value.Trim();
        }
        else
        {
            // 尝试从第一行提取
            var firstLine = stackTrace.Split('\n').FirstOrDefault()?.Trim() ?? "";
            exceptionInfo.ExceptionType = firstLine.Length > 100 ? 
                firstLine[..Math.Min(firstLine.Length, 100)] : firstLine;
        }

        // 检查内部异常
        if (InnerExceptionRegex.IsMatch(stackTrace))
        {
            var parts = InnerExceptionRegex.Split(stackTrace);
            if (parts.Length > 1)
            {
                exceptionInfo.InnerException = new ExceptionInfo
                {
                    RawText = parts[1].Trim(),
                    ExceptionType = "InnerException",
                    Message = "See inner exception stack trace"
                };
            }
        }

        return exceptionInfo;
    }

    public Dictionary<string, object> ExtractMetadata(string stackTrace)
    {
        var metadata = new Dictionary<string, object>();

        // 统计信息
        var lines = stackTrace.Split('\n');
        metadata["TotalLines"] = lines.Length;
        metadata["FrameCount"] = lines.Count(l => l.Trim().StartsWith("at "));
        metadata["HasNativeCode"] = NativeCodeRegex.IsMatch(stackTrace);
        metadata["HasInnerException"] = InnerExceptionRegex.IsMatch(stackTrace);

        // 提取异常数量
        var exceptionCount = ExceptionRegex.Matches(stackTrace).Count;
        metadata["ExceptionCount"] = exceptionCount;

        return metadata;
    }
}