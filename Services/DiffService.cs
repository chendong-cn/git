namespace NetConfigPro.Services;

public enum DiffKind
{
    Same,
    Added,
    Removed
}

public sealed class DiffLine
{
    public DiffKind Kind { get; init; }
    public string Text { get; init; } = "";
    public int? LeftNo { get; init; }
    public int? RightNo { get; init; }

    public string Marker => Kind switch
    {
        DiffKind.Added => "+",
        DiffKind.Removed => "-",
        _ => " "
    };
}

/// <summary>基于最长公共子序列的行级配置对比。</summary>
public static class DiffService
{
    private const long CellBudget = 1_600_000; // 超过该规模退化为逐行比较，避免内存暴涨

    public static List<DiffLine> Compare(string left, string right)
    {
        var a = Split(left);
        var b = Split(right);

        if ((long)a.Count * b.Count > CellBudget) return SimpleCompare(a, b);

        var n = a.Count;
        var m = b.Count;
        var dp = new int[n + 1, m + 1];

        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var result = new List<DiffLine>(n + m);
        var x = 0;
        var y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                result.Add(new DiffLine { Kind = DiffKind.Same, Text = a[x], LeftNo = x + 1, RightNo = y + 1 });
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
            {
                result.Add(new DiffLine { Kind = DiffKind.Removed, Text = a[x], LeftNo = x + 1 });
                x++;
            }
            else
            {
                result.Add(new DiffLine { Kind = DiffKind.Added, Text = b[y], RightNo = y + 1 });
                y++;
            }
        }

        while (x < n) result.Add(new DiffLine { Kind = DiffKind.Removed, Text = a[x], LeftNo = ++x });
        while (y < m) result.Add(new DiffLine { Kind = DiffKind.Added, Text = b[y], RightNo = ++y });

        return result;
    }

    private static List<DiffLine> SimpleCompare(List<string> a, List<string> b)
    {
        var result = new List<DiffLine>();
        var max = Math.Max(a.Count, b.Count);
        for (var i = 0; i < max; i++)
        {
            var l = i < a.Count ? a[i] : null;
            var r = i < b.Count ? b[i] : null;
            if (l == r) result.Add(new DiffLine { Kind = DiffKind.Same, Text = l ?? "", LeftNo = i + 1, RightNo = i + 1 });
            else
            {
                if (l != null) result.Add(new DiffLine { Kind = DiffKind.Removed, Text = l, LeftNo = i + 1 });
                if (r != null) result.Add(new DiffLine { Kind = DiffKind.Added, Text = r, RightNo = i + 1 });
            }
        }
        return result;
    }

    public static (int Added, int Removed, int Same) Stats(IEnumerable<DiffLine> lines)
    {
        var added = lines.Count(x => x.Kind == DiffKind.Added);
        var removed = lines.Count(x => x.Kind == DiffKind.Removed);
        var same = lines.Count(x => x.Kind == DiffKind.Same);
        return (added, removed, same);
    }

    private static List<string> Split(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(x => x.TrimEnd()).ToList();
}
