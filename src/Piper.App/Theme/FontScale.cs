using System.Drawing;
using Piper.Core.Sessions;

namespace Piper.App.Theme;

/// <summary>
/// The live UI font size, as a step away from the palette's base fonts, and the cache of the
/// <see cref="Font"/> instances that realise it.
/// </summary>
/// <remarks>
/// <para>
/// Fonts issued here are created once per (base font, step) pair and <b>never disposed</b>. Controls
/// and <c>ToolStripItem</c>s hold a raw reference to whatever font was assigned and WinForms never
/// clones one, so freeing the previous font on a zoom change would leave dangling GDI handles behind
/// in every control still pointing at it. The cache bounds the cost instead: four base fonts across
/// nine steps is at most 36 handles for the lifetime of the process, and
/// <see cref="Palette"/>'s fonts were already never-disposed statics.
/// </para>
/// <para>
/// <see cref="Rebase"/> exists because a control's current font cannot be used to derive its
/// unscaled size. A dialog opened while the UI is at 130% is built with fonts that are already
/// scaled; re-deriving from those would multiply the scale again on every re-apply. Every font this
/// class issues is therefore recorded against the base it came from, and re-applying looks the
/// instance up by reference rather than measuring it.
/// </para>
/// </remarks>
internal static class FontScale
{
    /// <summary>The unscaled fonts. Index into this array identifies a font across steps.</summary>
    private static readonly Font[] Bases =
    [
        new("Consolas", 9.5f, FontStyle.Regular, GraphicsUnit.Point),
        new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
        new("Consolas", 9.5f, FontStyle.Bold, GraphicsUnit.Point),
        new("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point),
    ];

    public const int Mono = 0;
    public const int Ui = 1;
    public const int MonoBold = 2;
    public const int UiBold = 3;

    private static readonly Dictionary<(int BaseIndex, int Step), Font> Cache = [];

    /// <summary>
    /// Every font handed out, mapped back to the base it was scaled from.
    /// </summary>
    /// <remarks>
    /// Keyed by reference on purpose. <see cref="Font"/> overrides equality by value -- family, size,
    /// style, unit -- so a plain dictionary would also match a font this class never issued, and
    /// <see cref="Rebase"/> would then quietly replace a font some control had deliberately been
    /// given. Only the instances handed out here may be rebased.
    /// </remarks>
    private static readonly Dictionary<Font, int> BaseOf = new(ReferenceEqualityComparer.Instance);

    private static int _step;

    /// <summary>
    /// Whether the Ctrl+MouseWheel gesture is live. The menu and keyboard shortcuts ignore this, so
    /// turning it off cannot leave the size unreachable.
    /// </summary>
    public static bool WheelEnabled { get; set; } = true;

    static FontScale()
    {
        for (var i = 0; i < Bases.Length; i++)
        {
            Cache[(i, 0)] = Bases[i];
            BaseOf[Bases[i]] = i;
        }
    }

    public static int Step => _step;

    public static float Multiplier => FontScaleSettingsStore.MultiplierFor(_step);

    /// <summary>The current size as a whole percentage, for display.</summary>
    public static int Percent => (int)Math.Round(Multiplier * 100);

    public static bool IsDefault => _step == 0;

    /// <summary>Sets the step, clamped. Returns whether anything changed.</summary>
    public static bool SetStep(int step)
    {
        var clamped = FontScaleSettingsStore.Clamp(step);
        if (clamped == _step) return false;
        _step = clamped;
        return true;
    }

    public static bool ZoomIn() => SetStep(_step + 1);

    public static bool ZoomOut() => SetStep(_step - 1);

    public static bool Reset() => SetStep(0);

    /// <summary>The current instance of one of the base fonts.</summary>
    public static Font Scaled(int baseIndex)
    {
        if (Cache.TryGetValue((baseIndex, _step), out var cached)) return cached;

        var baseFont = Bases[baseIndex];
        var scaled = new Font(baseFont.FontFamily, baseFont.SizeInPoints * Multiplier, baseFont.Style, GraphicsUnit.Point);
        Cache[(baseIndex, _step)] = scaled;
        BaseOf[scaled] = baseIndex;
        return scaled;
    }

    /// <summary>
    /// Returns the current instance of <paramref name="font"/>'s base font, or null when the font
    /// did not come from here and must be left alone.
    /// </summary>
    public static Font? Rebase(Font? font) =>
        font is not null && BaseOf.TryGetValue(font, out var baseIndex) ? Scaled(baseIndex) : null;
}
