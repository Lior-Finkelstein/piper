using System.Text.Json;

namespace Piper.Core.Sessions;

/// <summary>
/// The user's chosen UI font size, as a step away from the application default. Stored separately
/// from captured sessions so the next application session opens at the same size.
/// </summary>
public sealed class FontScaleSettings
{
    public int Step { get; set; }
}

/// <summary>
/// Persists the UI font-size step under the user's local app-data directory, and owns the arithmetic
/// that turns a step into a font multiplier.
/// </summary>
/// <remarks>
/// The range is deliberately clamped. Fiddler Classic takes an unbounded point size and its layout
/// breaks at the extremes -- menus clip, dialogs overlap -- because a WinForms form built from fixed
/// pixel heights cannot absorb arbitrary text growth. Fiddler Everywhere clamps to 70-150% instead,
/// which is the range mirrored here.
///
/// Like the other preference stores, this is convenience state: missing, inaccessible, or malformed
/// data falls back to the application default rather than failing.
/// </remarks>
public static class FontScaleSettingsStore
{
    /// <summary>Smallest step, i.e. 70% of the default font size.</summary>
    public const int MinStep = -3;

    /// <summary>Largest step, i.e. 150% of the default font size.</summary>
    public const int MaxStep = 5;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Piper", "font-scale.json");

    public static int Clamp(int step) => Math.Clamp(step, MinStep, MaxStep);

    /// <summary>
    /// The font multiplier for a step, in 10% increments. Step 0 returns exactly 1, so the default
    /// size is bit-for-bit the font the application would have used without this feature.
    /// </summary>
    public static float MultiplierFor(int step)
    {
        var clamped = Clamp(step);
        return clamped == 0 ? 1f : 1f + (clamped * 0.1f);
    }

    public static void Save(FontScaleSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        path ??= DefaultPath;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(settings));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Loads the saved step, clamped. A hand-edited or corrupted file can therefore never leave the
    /// application at an unusable size.
    /// </summary>
    public static FontScaleSettings? Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path)) return null;
            if (JsonSerializer.Deserialize<FontScaleSettings>(File.ReadAllText(path)) is not { } settings) return null;
            settings.Step = Clamp(settings.Step);
            return settings;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
