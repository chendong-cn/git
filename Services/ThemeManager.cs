using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace NetConfigPro.Services;

/// <summary>
/// 皮肤管理：内置 32 种主色，运行时动态构建一个覆盖资源字典
/// （键名与 Themes/Tech.xaml 中的主色系键一致），插入到
/// Application.Resources.MergedDictionaries 末尾以获得最高查找优先级。
/// 控件模板全部通过 StaticResource 引用这些键，换肤后重建主窗口即可整体生效。
/// </summary>
public static class ThemeManager
{
    /// <summary>一种皮肤：中文名 + 主色。</summary>
    public readonly record struct ThemeOption(string Name, Color Main);

    // 32 种预置主色（均为中深色，保证浅色主题上的文字/描边对比度）
    private static readonly (string Name, string Hex)[] PaletteDefs =
    [
        ("科技蓝", "#0C7FDB"),
        ("深海蓝", "#1D4ED8"),
        ("靛蓝",   "#4338CA"),
        ("群青",   "#4F46E5"),
        ("紫罗兰", "#6D28D9"),
        ("亮紫",   "#7C3AED"),
        ("深紫",   "#7E22CE"),
        ("洋红",   "#C026D3"),
        ("品红",   "#BE185D"),
        ("玫红",   "#DB2777"),
        ("酒红",   "#9F1239"),
        ("中国红", "#B91C1C"),
        ("朱砂",   "#C2410C"),
        ("橙色",   "#EA580C"),
        ("琥珀",   "#B45309"),
        ("金黄",   "#A16207"),
        ("橄榄",   "#3F6212"),
        ("草绿",   "#4D7C0F"),
        ("森林绿", "#15803D"),
        ("翠绿",   "#047857"),
        ("竹青",   "#0F766E"),
        ("青碧",   "#0D9488"),
        ("孔雀蓝", "#0E7490"),
        ("天青",   "#0369A1"),
        ("晴空蓝", "#0284C7"),
        ("蔚蓝",   "#2563EB"),
        ("藏青",   "#1E3A8A"),
        ("海军蓝", "#3730A3"),
        ("钢青",   "#155E75"),
        ("石板蓝", "#475569"),
        ("石墨灰", "#3F3F46"),
        ("墨黑",   "#27272A")
    ];

    private static readonly List<ThemeOption> Themes = PaletteDefs
        .Select(p => new ThemeOption(p.Name, ParseColor(p.Hex)))
        .ToList();

    public static IReadOnlyList<ThemeOption> Options => Themes;

    /// <summary>当前应用的主题索引（默认 0 = 科技蓝）。</summary>
    public static int CurrentIndex { get; private set; }

    /// <summary>已插入 App 资源中的覆盖字典（换肤时移除旧实例，避免键重复累积）。</summary>
    private static ResourceDictionary? _active;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string SettingsFile => Path.Combine(AppStorage.DataDir, "theme.json");

    // ==================== 生命周期 ====================

    /// <summary>启动时调用（必须在创建主窗口之前）：读取存档 → 构建覆盖字典并注入 App 资源。</summary>
    public static int ApplySavedTheme()
    {
        CurrentIndex = LoadThemeIndex();
        ApplyCore();
        return CurrentIndex;
    }

    /// <summary>切换到指定主题：替换覆盖字典内容。</summary>
    public static void Apply(int index)
    {
        CurrentIndex = Math.Clamp(index, 0, Themes.Count - 1);
        ApplyCore();
    }

    public static void SaveThemeIndex(int index)
    {
        try
        {
            Directory.CreateDirectory(AppStorage.DataDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new { theme = index }, JsonOpts));
        }
        catch
        {
            // 主题保存失败不影响使用
        }
    }

    private static int LoadThemeIndex()
    {
        // [测试用] 环境变量可覆盖：NCP_THEME=0..31
        if (Environment.GetEnvironmentVariable("NCP_THEME") is { } env &&
            int.TryParse(env, out var idx) && idx >= 0 && idx < Themes.Count)
            return idx;
        try
        {
            if (!File.Exists(SettingsFile)) return 0;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile));
            return doc.RootElement.TryGetProperty("theme", out var el) && el.TryGetInt32(out var v)
                ? Math.Clamp(v, 0, Themes.Count - 1)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void ApplyCore()
    {
        var dict = BuildDictionary(Themes[CurrentIndex].Main);
        var merged = Application.Current.Resources.MergedDictionaries;
        if (_active is not null)
        {
            // 先加入新字典（末尾优先），再移除旧字典，避免 DynamicResource 中途回落到默认色造成闪烁
            merged.Add(dict);
            merged.Remove(_active);
        }
        else
        {
            merged.Add(dict);
        }
        _active = dict;
    }

    // ==================== 覆盖字典构建 ====================

    /// <summary>
    /// 依据主色生成一套与 Tech.xaml 同名的键（主色 + 派生浅色/深色 + 渐变 + 发光）。
    /// 合并字典末尾优先查找，因此这些键会覆盖 Tech.xaml 中的默认值。
    /// </summary>
    public static ResourceDictionary BuildDictionary(Color main)
    {
        var soft = Blend(main, Colors.White, 0.84);   // 选中填充（浅）
        var hover = Blend(main, Colors.White, 0.91);  // 悬停填充（更浅）
        var dark = Blend(main, Colors.Black, 0.16);   // 描边/渐变终点（深）

        var d = new ResourceDictionary();

        d["AccentCyan"] = main;
        d["SelectedFill"] = soft;
        d["HoverFill"] = hover;
        d["AccentDark"] = dark;

        d["AccentCyanBrush"] = new SolidColorBrush(main);
        d["SelectedFillBrush"] = new SolidColorBrush(soft);
        d["HoverFillBrush"] = new SolidColorBrush(hover);
        d["AccentDarkBrush"] = new SolidColorBrush(dark);

        d["HeaderGradient"] = new LinearGradientBrush(Blend(main, Colors.White, 0.10), dark, 0);
        d["AccentGradient"] = new LinearGradientBrush(Blend(main, Colors.White, 0.08), dark, 0);

        d["GlowCyan"] = new DropShadowEffect
        {
            Color = main,
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0.22
        };

        return d;
    }

    /// <summary>按百分比把 base 与 overlay 混合（pct=0 全 base，pct=1 全 overlay）。</summary>
    private static Color Blend(Color baseColor, Color overlay, double pct)
    {
        var inv = 1 - pct;
        return Color.FromRgb(
            (byte)(baseColor.R * inv + overlay.R * pct + 0.5),
            (byte)(baseColor.G * inv + overlay.G * pct + 0.5),
            (byte)(baseColor.B * inv + overlay.B * pct + 0.5));
    }

    private static Color ParseColor(string hex)
    {
        var s = hex.TrimStart('#');
        return Color.FromRgb(
            Convert.ToByte(s[..2], 16),
            Convert.ToByte(s[2..4], 16),
            Convert.ToByte(s[4..6], 16));
    }

    /// <summary>hex 形式展示主色（用于色板提示）。</summary>
    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
