namespace CrashStackAnalyzer.Models;

/// <summary>
/// 异常信息模型
/// </summary>
public class ExceptionInfo
{
    /// <summary>
    /// 异常类型（如 NullReferenceException）
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// 异常消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 原始异常文本
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// 是否有内部异常
    /// </summary>
    public bool HasInnerException => InnerException != null;

    /// <summary>
    /// 内部异常
    /// </summary>
    public ExceptionInfo? InnerException { get; set; }

    /// <summary>
    /// 异常链深度（包含当前异常）
    /// </summary>
    public int ExceptionChainDepth
    {
        get
        {
            var depth = 1;
            var current = InnerException;
            while (current != null)
            {
                depth++;
                current = current.InnerException;
            }
            return depth;
        }
    }

    /// <summary>
    /// 获取所有异常（包括内部异常）
    /// </summary>
    public IEnumerable<ExceptionInfo> GetAllExceptions()
    {
        yield return this;
        
        var current = InnerException;
        while (current != null)
        {
            yield return current;
            current = current.InnerException;
        }
    }

    /// <summary>
    /// 获取根因异常（最内部的异常）
    /// </summary>
    public ExceptionInfo GetRootCause()
    {
        var current = this;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }
        return current;
    }

    public override string ToString()
    {
        var result = $"{ExceptionType}: {Message}";
        
        if (InnerException != null)
        {
            result += $" ---> {InnerException}";
        }
        
        return result;
    }

    /// <summary>
    /// 获取完整的异常链描述
    /// </summary>
    public string GetFullChainDescription()
    {
        var exceptions = GetAllExceptions().ToList();
        var lines = new List<string>();

        for (int i = 0; i < exceptions.Count; i++)
        {
            var prefix = i == 0 ? "Exception" : new string(' ', i * 2) + "└─ Inner Exception";
            lines.Add($"{prefix}: {exceptions[i]}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}