namespace PowerPlanSwitcher;

using Microsoft.Win32;
using PowerPlanSwitcher.Properties;

internal enum ColorTheme
{
    System,
    Light,
    Dark,
}

internal static class ColorThemeHelper
{
    private static readonly string WindowsColorThemeKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static SynchronizationContext? uiContext;
    private static int systemThemeApplyGeneration;

    public static event EventHandler? ApplicationColorModeChanged;

    // 枚举 → 本地化显示名
    // Enum -> localized display name
    public static string GetDisplayName(ColorTheme theme) => theme switch
    {
        ColorTheme.System => Strings.ColorTheme_System,
        ColorTheme.Light => Strings.ColorTheme_Light,
        ColorTheme.Dark => Strings.ColorTheme_Dark,
        _ => theme.ToString(),
    };

    // 显示名列表（顺序与 GetColorThemes 一致）
    // Display name list (order matches GetColorThemes)
    public static IEnumerable<string> GetDisplayNames() =>
        GetColorThemes().Select(GetDisplayName);

    // 枚举列表，顺序固定
    // Enum list, order is fixed
    public static IList<ColorTheme> GetColorThemes() =>
        Enum.GetValues<ColorTheme>();

    // 读取已保存的枚举名；兼容旧版英文显示名
    // Read saved enum name; compatible with legacy English display names
    public static ColorTheme GetSelectedColorTheme()
    {
        var saved = Settings.Default.ColorTheme;
        if (string.IsNullOrEmpty(saved))
        {
            return ColorTheme.System;
        }

        if (Enum.TryParse<ColorTheme>(saved, out var theme))
        {
            return theme;
        }

        // 兼容旧格式：英文显示名
        // Legacy format: English display name
        return saved switch
        {
            "Use System Setting" => ColorTheme.System,
            "Light Mode" => ColorTheme.Light,
            "Dark Mode" => ColorTheme.Dark,
            _ => ColorTheme.System,
        };
    }

    // 保存为枚举名（语言无关）
    // Save as enum name (language independent)
    public static void SetSelectedColorTheme(ColorTheme theme)
    {
        Settings.Default.ColorTheme = theme.ToString();
    }

    public static ColorTheme GetActiveColorTheme()
    {
        var colorTheme = GetSelectedColorTheme();
        if (colorTheme != ColorTheme.System)
        {
            return colorTheme;
        }

        using var key = Registry.CurrentUser.OpenSubKey(WindowsColorThemeKey);
        if ((key?.GetValue("SystemUsesLightTheme") as int?
            ?? 1)
            == 1)
        {
            return ColorTheme.Light;
        }
        return ColorTheme.Dark;
    }

    public static void Initialize()
    {
        uiContext = SynchronizationContext.Current;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void ApplyToApplication()
    {
        var colorMode = GetActiveColorTheme() == ColorTheme.Dark
            ? SystemColorMode.Dark
            : SystemColorMode.Classic;

        if (Application.ColorMode == colorMode)
        {
            return;
        }

        Application.SetColorMode(colorMode);
        ApplicationColorModeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void ApplyToDataGridView(DataGridView grid)
    {
        if (!Application.IsDarkModeEnabled)
        {
            return;
        }

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SystemColors.Control;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = SystemColors.ControlText;
        grid.RowHeadersDefaultCellStyle.BackColor = SystemColors.Control;
        grid.RowHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
        grid.BackgroundColor = SystemColors.Window;
        grid.GridColor = SystemColors.ControlDark;
        grid.DefaultCellStyle.BackColor = SystemColors.Window;
        grid.DefaultCellStyle.ForeColor = SystemColors.WindowText;
        grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
        grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
    }

    private static void OnUserPreferenceChanged(
        object sender,
        UserPreferenceChangedEventArgs e)
    {
        if (GetSelectedColorTheme() != ColorTheme.System)
        {
            return;
        }

        if (e.Category is not (UserPreferenceCategory.General
            or UserPreferenceCategory.Color))
        {
            return;
        }

        var generation = Interlocked.Increment(ref systemThemeApplyGeneration);
        _ = Task.Delay(150).ContinueWith(_ =>
        {
            if (generation != Volatile.Read(ref systemThemeApplyGeneration))
            {
                return;
            }

            void apply() => ApplyToApplication();
            if (uiContext is not null)
            {
                uiContext.Post(_ => apply(), null);
                return;
            }

            apply();
        });
    }
}
