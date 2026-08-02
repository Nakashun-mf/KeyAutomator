using System.Globalization;
using KeyAutomator.Models;

namespace KeyAutomator.Services;

/// <summary>
/// フラットな actions 上の repeat / end_repeat ブロックを扱う。
/// </summary>
public static class RepeatBlock
{
    public const string StartType = "repeat";
    public const string EndType = "end_repeat";
    public const int DefaultCount = 2;
    public const int MaxCount = 9999;

    public static bool IsStart(string? type) =>
        string.Equals(type, StartType, StringComparison.OrdinalIgnoreCase);

    public static bool IsEnd(string? type) =>
        string.Equals(type, EndType, StringComparison.OrdinalIgnoreCase);

    public static bool IsMarker(string? type) => IsStart(type) || IsEnd(type);

    public static bool TryParseCount(string? value, out int count)
    {
        count = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) &&
            !double.TryParse(value, out n))
        {
            return false;
        }

        if (n < 1 || n > MaxCount || Math.Abs(n - Math.Round(n)) > 0.001)
            return false;

        count = (int)Math.Round(n);
        return true;
    }

    /// <summary>
    /// startIndex の repeat に対応する end_repeat のインデックス。無ければ -1。
    /// </summary>
    public static int FindMatchingEnd(IReadOnlyList<ActionItem> actions, int startIndex)
    {
        if (startIndex < 0 || startIndex >= actions.Count || !IsStart(actions[startIndex].Type))
            return -1;

        var depth = 0;
        for (var i = startIndex + 1; i < actions.Count; i++)
        {
            if (IsStart(actions[i].Type))
            {
                depth++;
            }
            else if (IsEnd(actions[i].Type))
            {
                if (depth == 0)
                    return i;
                depth--;
            }
        }

        return -1;
    }

    /// <summary>
    /// 各アクションの表示用ネスト深さ（repeat 行は開始深さ、end_repeat は対応開始と同じ深さ）。
    /// </summary>
    public static int[] ComputeDepths(IReadOnlyList<ActionItem> actions)
    {
        var depths = new int[actions.Count];
        var depth = 0;
        for (var i = 0; i < actions.Count; i++)
        {
            if (IsEnd(actions[i].Type))
                depth = Math.Max(0, depth - 1);

            depths[i] = depth;

            if (IsStart(actions[i].Type))
                depth++;
        }

        return depths;
    }

    public static bool TryValidate(IReadOnlyList<ActionItem> actions, out string error)
    {
        error = string.Empty;
        var open = 0;
        for (var i = 0; i < actions.Count; i++)
        {
            var type = actions[i].Type;
            if (IsStart(type))
            {
                if (!TryParseCount(actions[i].Value, out _))
                {
                    error = $"手順 {i + 1}: 繰り返し回数は 1〜{MaxCount} の整数で指定してください";
                    return false;
                }

                open++;
            }
            else if (IsEnd(type))
            {
                if (open == 0)
                {
                    error = $"手順 {i + 1}: 「ここまで」に対応する「繰り返し」がありません";
                    return false;
                }

                open--;
            }
        }

        if (open > 0)
        {
            error = $"「繰り返し」が {open} 個閉じられていません（「ここまで」を追加してください）";
            return false;
        }

        return true;
    }
}
