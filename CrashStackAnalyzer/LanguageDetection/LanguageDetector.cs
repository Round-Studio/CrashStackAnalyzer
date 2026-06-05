using System.Text.RegularExpressions;
using CrashStackAnalyzer.Models;

namespace CrashStackAnalyzer.LanguageDetection;

/// <summary>
/// 语言检测器
/// </summary>
public class LanguageDetector
{
    private readonly Dictionary<LanguageType, List<Func<string, double>>> _featureExtractors;

    public LanguageDetector()
    {
        _featureExtractors = new Dictionary<LanguageType, List<Func<string, double>>>
        {
            [LanguageType.CSharp] = new List<Func<string, double>>
            {
                text => CalculateFeatureScore(text, @"\bin\s+\w:[\\/].*?:line\s+\d+", 0.4),
                text => CalculateFeatureScore(text, @"\b(System\.|Microsoft\.)", 0.3),
                text => CalculateFeatureScore(text, @"\bNullable`1\b", 0.15),
                text => CalculateFeatureScore(text, @"\.(cs|csx):line\s+\d+", 0.15),
            },
            [LanguageType.Java] = new List<Func<string, double>>
            {
                text => CalculateFeatureScore(text, @"\b(com\.|org\.|net\.)\w+", 0.3),
                text => CalculateFeatureScore(text, @"\.java:\d+\)", 0.3),
                text => CalculateFeatureScore(text, @"Caused by:|Suppressed:", 0.2),
                text => CalculateFeatureScore(text, @"\b(java\.|javax\.|sun\.)\w+", 0.2),
            }
        };
    }

    /// <summary>
    /// 检测堆栈文本的主要语言
    /// </summary>
    public LanguageType Detect(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return LanguageType.Unknown;

        var scores = new Dictionary<LanguageType, double>();

        foreach (var language in _featureExtractors.Keys)
        {
            scores[language] = CalculateLanguageScore(stackTrace, _featureExtractors[language]);
        }

        var bestMatch = scores.OrderByDescending(x => x.Value).First();

        // 如果最高分太低，返回 Unknown
        return bestMatch.Value > 0.2 ? bestMatch.Key : LanguageType.Unknown;
    }

    private double CalculateLanguageScore(string text, List<Func<string, double>> extractors)
    {
        return extractors.Sum(extractor => extractor(text));
    }

    private double CalculateFeatureScore(string text, string pattern, double weight)
    {
        if (string.IsNullOrEmpty(pattern))
            return 0;

        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
        if (matches.Count == 0)
            return 0;

        // 使用对数函数避免大量匹配导致的分数过高
        return weight * Math.Min(1.0, Math.Log2(matches.Count + 1) / 2);
    }

    /// <summary>
    /// 获取所有可能的语言及其置信度
    /// </summary>
    public Dictionary<LanguageType, double> GetLanguageScores(string stackTrace)
    {
        var scores = new Dictionary<LanguageType, double>();

        foreach (var language in _featureExtractors.Keys)
        {
            scores[language] = CalculateLanguageScore(stackTrace, _featureExtractors[language]);
        }

        return scores;
    }
}