using Piper.Core.Sessions;

internal static class FontScaleStoreTests
{
    public static Task RunAsync(TestRunner runner) => runner.RunAsync("font scale persists, clamps, and stays renderable", () =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"piper-font-scale-{Guid.NewGuid():N}.json");
        try
        {
            FontScaleSettingsStore.Save(new FontScaleSettings { Step = 3, WheelZoomEnabled = false }, path);
            var restored = FontScaleSettingsStore.Load(path);
            runner.IsTrue(restored is not null, "saved step can be loaded");
            runner.AreEqual(3, restored!.Step, "step round trips");
            runner.AreEqual(false, restored.WheelZoomEnabled, "the Ctrl+MouseWheel toggle round trips");

            // The gesture is on unless it was deliberately turned off, so a file written before the
            // toggle existed must not silently disable it.
            runner.AreEqual(true, new FontScaleSettings().WheelZoomEnabled, "the toggle defaults to on");
            File.WriteAllText(path, """{"Step":2}""");
            var legacy = FontScaleSettingsStore.Load(path);
            runner.AreEqual(2, legacy!.Step, "a file predating the toggle still loads its step");
            runner.AreEqual(true, legacy.WheelZoomEnabled, "and leaves the gesture enabled");

            // Clamping is what keeps the layout usable, so the bounds are asserted directly.
            runner.AreEqual(FontScaleSettingsStore.MaxStep, FontScaleSettingsStore.Clamp(FontScaleSettingsStore.MaxStep + 1),
                "a step past the maximum clamps down");
            runner.AreEqual(FontScaleSettingsStore.MinStep, FontScaleSettingsStore.Clamp(FontScaleSettingsStore.MinStep - 1),
                "a step past the minimum clamps up");
            runner.AreEqual(FontScaleSettingsStore.MaxStep, FontScaleSettingsStore.Clamp(int.MaxValue), "int.MaxValue clamps");
            runner.AreEqual(FontScaleSettingsStore.MinStep, FontScaleSettingsStore.Clamp(int.MinValue), "int.MinValue clamps");

            // The default must be the exact font the application would use without this feature.
            runner.AreEqual(1f, FontScaleSettingsStore.MultiplierFor(0), "step 0 is exactly 1x");

            var previous = float.MinValue;
            for (var step = FontScaleSettingsStore.MinStep; step <= FontScaleSettingsStore.MaxStep; step++)
            {
                var multiplier = FontScaleSettingsStore.MultiplierFor(step);
                runner.IsTrue(multiplier > previous, $"multiplier increases at step {step}");
                previous = multiplier;

                // Font rejects a size of zero or less, and an absurdly large one is unusable.
                // 9.5pt is the largest base font in the palette.
                var size = 9.5f * multiplier;
                runner.IsTrue(size is >= 6f and <= 24f, $"step {step} yields a renderable {size:0.##}pt");
            }

            runner.IsTrue(FontScaleSettingsStore.MultiplierFor(int.MaxValue) == FontScaleSettingsStore.MultiplierFor(FontScaleSettingsStore.MaxStep),
                "an out-of-range step still yields a clamped multiplier");

            // A hand-edited file must not be able to leave the UI unusable.
            File.WriteAllText(path, """{"Step":999}""");
            runner.AreEqual(FontScaleSettingsStore.MaxStep, FontScaleSettingsStore.Load(path)!.Step, "an absurd saved step is clamped on load");

            File.WriteAllText(path, "not json");
            runner.AreEqual<FontScaleSettings?>(null, FontScaleSettingsStore.Load(path), "corrupt settings are ignored");

            File.Delete(path);
            runner.AreEqual<FontScaleSettings?>(null, FontScaleSettingsStore.Load(path), "a missing file is ignored");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        return Task.CompletedTask;
    });
}
