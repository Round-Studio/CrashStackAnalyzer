using System.Text.RegularExpressions;

namespace CrashStackAnalyzer.Normalization;

/// <summary>
/// 堆栈文本标准化器
/// </summary>
public class StackTraceNormalizer
{
    private static readonly Regex[] LineEndingPatterns = new[]
    {
        new Regex(@"\r\n"),
        new Regex(@"\n")
    };

    private static readonly Regex[] WhitespacePatterns = new[]
    {
        new Regex(@"[ \t]+", RegexOptions.Multiline),
        new Regex(@"^\s+|\s+$", RegexOptions.Multiline)
    };

    /// <summary>
    /// 标准化堆栈文本
    /// </summary>
    public string Normalize(string rawStackTrace)
    {
        if (string.IsNullOrWhiteSpace(rawStackTrace))
            return string.Empty;

        var text = rawStackTrace;

        // 统一换行符
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // 标准化空白字符
        text = WhitespacePatterns[0].Replace(text, " ");
        text = WhitespacePatterns[1].Replace(text, "");

        // 移除多余的空行
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // 移除 BOM 和其他不可见字符
        text = text.TrimStart('\uFEFF', '\u200B');

        return text.Trim();
    }

    /// <summary>
    /// 分割为行并清理
    /// </summary>
    public List<string> SplitAndCleanLines(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
            return new List<string>();

        return normalizedText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    /// <summary>
    /// 移除重复的堆栈行（递归调用）
    /// </summary>
    public string RemoveDuplicates(string stackTrace)
    {
        var lines = SplitAndCleanLines(stackTrace);
        var seen = new HashSet<string>();
        var uniqueLines = new List<string>();

        foreach (var line in lines)
        {
            // 规范化行以进行比较
            var normalizedLine = NormalizeLine(line);
            if (seen.Add(normalizedLine))
            {
                uniqueLines.Add(line);
            }
        }

        return string.Join("\n", uniqueLines);
    }

    private string NormalizeLine(string line)
    {
        // 移除变量地址和动态值
        return Regex.Replace(line, @"0x[0-9a-fA-F]+", "0xADDR")
                   .Replace(" ", "");
    }
}