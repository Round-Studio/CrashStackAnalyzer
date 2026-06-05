using System.Text;

namespace CrashStackAnalyzer.Models;

/// <summary>
/// 完整的崩溃报告
/// </summary>
public class CrashReport
{
    /// <summary>
    /// 原始堆栈文本
    /// </summary>
    public string RawStackTrace { get; set; } = string.Empty;

    /// <summary>
    /// 主要语言
    /// </summary>
    public LanguageType PrimaryLanguage { get; set; }

    /// <summary>
    /// 主异常信息
    /// </summary>
    public ExceptionInfo MainException { get; set; } = new();

    /// <summary>
    /// 堆栈帧列表
    /// </summary>
    public List<StackFrame> Frames { get; set; } = new();

    /// <summary>
    /// 内部异常列表
    /// </summary>
    public List<ExceptionInfo> InnerExceptions { get; set; } = new();

    /// <summary>
    /// 元数据（如线程信息、进程ID等）
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 解析时间
    /// </summary>
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 用户代码帧
    /// </summary>
    public IEnumerable<StackFrame> UserCodeFrames => Frames.Where(f => f.IsUserCode());

    /// <summary>
    /// 系统代码帧
    /// </summary>
    public IEnumerable<StackFrame> SystemCodeFrames => Frames.Where(f => !f.IsUserCode());

    /// <summary>
    /// 异常发生点
    /// </summary>
    public StackFrame? ExceptionPoint => StackFrameExtensions.GetExceptionPoint(Frames);

    /// <summary>
    /// 报告是否有效
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(RawStackTrace) && Frames.Count > 0;

    /// <summary>
    /// 验证报告完整性，返回所有发现的问题
    /// </summary>
    /// <returns>问题列表，如果没有问题则为空列表</returns>
    public List<string> Validate()
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(RawStackTrace))
            issues.Add("Raw stack trace is empty");

        if (PrimaryLanguage == LanguageType.Unknown)
            issues.Add("Could not detect programming language");

        if (Frames.Count == 0)
            issues.Add("No stack frames parsed");

        if (string.IsNullOrWhiteSpace(MainException.ExceptionType))
            issues.Add("No exception type extracted");

        if (!UserCodeFrames.Any())
            issues.Add("No user code frames found (all frames are system code)");

        return issues;
    }

    public override string ToString()
    {
        var frames = string.Join(Environment.NewLine, Frames.Select(f => "  " + f));
        return $"[{PrimaryLanguage}] {MainException}{Environment.NewLine}{frames}";
    }

    /// <summary>
    /// 获取格式化的报告摘要
    /// </summary>
    public string GetSummary()
    {
        return $"Language: {PrimaryLanguage}\n" +
               $"Exception: {MainException.ExceptionType}\n" +
               $"Message: {MainException.Message}\n" +
               $"Total Frames: {Frames.Count}\n" +
               $"User Code Frames: {UserCodeFrames.Count()}\n" +
               $"Has Source Info: {Frames.Any(f => f.FilePath != null)}\n" +
               $"Has Inner Exception: {MainException.HasInnerException}\n" +
               $"Parsed At: {ParsedAt:yyyy-MM-dd HH:mm:ss}";
    }

    /// <summary>
    /// 获取完整的报告文本
    /// </summary>
    public string GetFullReport()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("=== Crash Report ===");
        sb.AppendLine(GetSummary());
        sb.AppendLine();
        
        sb.AppendLine("--- Exception Chain ---");
        WriteExceptionChain(sb, MainException, 0);
        sb.AppendLine();
        
        sb.AppendLine("--- User Code Stack Trace ---");
        sb.AppendLine(UserCodeFrames.ToFormattedString(true));
        
        if (SystemCodeFrames.Any())
        {
            sb.AppendLine();
            sb.AppendLine("--- System Code Stack Trace ---");
            sb.AppendLine(SystemCodeFrames.ToFormattedString());
        }
        
        if (Metadata.Any())
        {
            sb.AppendLine();
            sb.AppendLine("--- Additional Metadata ---");
            foreach (var kvp in Metadata.OrderBy(k => k.Key))
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
        }

        return sb.ToString();
    }

    private void WriteExceptionChain(StringBuilder sb, ExceptionInfo exception, int level)
    {
        var indent = new string(' ', level * 2);
        sb.AppendLine($"{indent}{exception.ExceptionType}: {exception.Message}");
        
        if (exception.InnerException != null)
        {
            sb.AppendLine($"{indent}--- Inner Exception ---");
            WriteExceptionChain(sb, exception.InnerException, level + 1);
        }
    }
}