using CrashStackAnalyzer.Models;

namespace CrashStackAnalyzer.Parsers;

/// <summary>
/// 堆栈解析器接口
/// </summary>
public interface IStackParser
{
    /// <summary>
    /// 解析器支持的语言
    /// </summary>
    LanguageType SupportedLanguage { get; }

    /// <summary>
    /// 判断是否能解析该文本
    /// </summary>
    bool CanParse(string stackTrace);

    /// <summary>
    /// 解析堆栈文本为帧列表
    /// </summary>
    List<StackFrame> ParseFrames(string stackTrace);

    /// <summary>
    /// 提取异常信息
    /// </summary>
    ExceptionInfo ExtractException(string stackTrace);

    /// <summary>
    /// 提取元数据
    /// </summary>
    Dictionary<string, object> ExtractMetadata(string stackTrace);
}