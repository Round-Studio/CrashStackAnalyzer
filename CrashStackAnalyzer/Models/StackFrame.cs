using System.Text;

namespace CrashStackAnalyzer.Models;

/// <summary>
/// 堆栈帧信息
/// </summary>
public class StackFrame
{
    /// <summary>
    /// 原始文本
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// 方法名（含参数）
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 行号
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// 类名（含命名空间）
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// 模块名（DLL/JAR）
    /// </summary>
    public string? ModuleName { get; set; }

    /// <summary>
    /// 命名空间
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// 是否为本地代码
    /// </summary>
    public bool IsNativeCode { get; set; }

    /// <summary>
    /// 参数列表
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// 语言类型
    /// </summary>
    public LanguageType Language { get; set; } = LanguageType.Unknown;

    /// <summary>
    /// 附加元数据（如匿名类、异步方法等标志）
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    public override string ToString()
    {
        var result = $"at {MethodName}";
        
        if (FilePath != null && LineNumber != null)
        {
            result += $" in {FilePath}:line {LineNumber}";
        }
        else if (IsNativeCode)
        {
            result += " (Native Code)";
        }
        else if (ModuleName != null)
        {
            result += $" [{ModuleName}]";
        }

        // 添加特殊标记
        if (Metadata.TryGetValue("IsAsync", out var isAsync) && isAsync is true)
        {
            result += " [async]";
        }
        if (Metadata.TryGetValue("IsAnonymousClass", out var isAnon) && isAnon is true)
        {
            result += " [anonymous]";
        }

        return result;
    }

    /// <summary>
    /// 获取堆栈帧的简短描述
    /// </summary>
    public string GetShortDescription()
    {
        if (ClassName != null)
        {
            var methodName = MethodName.Contains('(') 
                ? MethodName[..MethodName.IndexOf('(')] 
                : MethodName;
            return $"{ClassName}.{methodName}()";
        }
        return MethodName.Contains('(') 
            ? MethodName[..MethodName.IndexOf('(')] + "()" 
            : MethodName;
    }

    /// <summary>
    /// 检查是否为用户代码（非系统/框架代码）
    /// </summary>
    public bool IsUserCode()
    {
        if (string.IsNullOrEmpty(Namespace))
            return false;

        // 排除常见的系统/框架命名空间
        var systemNamespaces = new[]
        {
            "System", "Microsoft", "java", "javax", "sun", "com.sun",
            "android", "org.springframework", "org.hibernate"
        };

        return !systemNamespaces.Any(ns => 
            Namespace.StartsWith(ns, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取源代码位置信息
    /// </summary>
    public string GetSourceLocation()
    {
        if (FilePath == null)
            return "Unknown location";

        if (LineNumber.HasValue)
            return $"{FilePath}:{LineNumber.Value}";

        return FilePath;
    }

    /// <summary>
    /// 设置元数据值
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        Metadata[key] = value;
    }

    /// <summary>
    /// 获取元数据值
    /// </summary>
    public T? GetMetadata<T>(string key) where T : class
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return null;
    }

    /// <summary>
    /// 获取元数据值（值类型版本）
    /// </summary>
    public T? GetMetadataValue<T>(string key) where T : struct
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return null;
    }
}

/// <summary>
/// 堆栈帧扩展方法
/// </summary>
public static class StackFrameExtensions
{
    /// <summary>
    /// 过滤用户代码帧
    /// </summary>
    public static IEnumerable<StackFrame> GetUserCodeFrames(this IEnumerable<StackFrame> frames)
    {
        return frames.Where(f => f.IsUserCode());
    }

    /// <summary>
    /// 过滤系统/框架代码帧
    /// </summary>
    public static IEnumerable<StackFrame> GetSystemCodeFrames(this IEnumerable<StackFrame> frames)
    {
        return frames.Where(f => !f.IsUserCode());
    }

    /// <summary>
    /// 按命名空间分组
    /// </summary>
    public static ILookup<string?, StackFrame> GroupByNamespace(this IEnumerable<StackFrame> frames)
    {
        return frames.ToLookup(f => f.Namespace);
    }

    /// <summary>
    /// 获取异常发生点（第一个用户代码帧）
    /// </summary>
    public static StackFrame? GetExceptionPoint(this IEnumerable<StackFrame> frames)
    {
        return frames.FirstOrDefault(f => f.IsUserCode()) ?? frames.FirstOrDefault();
    }

    /// <summary>
    /// 获取异常发生点（从 List 中）
    /// </summary>
    public static StackFrame? GetExceptionPoint(this List<StackFrame> frames)
    {
        return GetExceptionPoint((IEnumerable<StackFrame>)frames);
    }

    /// <summary>
    /// 转换为格式化的字符串
    /// </summary>
    public static string ToFormattedString(this IEnumerable<StackFrame> frames, bool includeMetadata = false)
    {
        var sb = new StringBuilder();
        var frameList = frames.ToList();

        for (int i = 0; i < frameList.Count; i++)
        {
            var frame = frameList[i];
            var prefix = i == 0 ? "→ " : "  ";
            sb.Append(prefix);
            sb.Append(frame);
            
            if (includeMetadata && frame.Metadata.Any())
            {
                var metadata = string.Join(", ", frame.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                sb.Append($" [{metadata}]");
            }
            
            if (i < frameList.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}