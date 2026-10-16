# Documentation screenshot fixture

This optional entry point builds the current application UI with a separate portable demo profile. It creates three fictitious connections, a complete stock light palette and a standard list layout with tags and notes hidden. It never copies the daily profile, credentials or connection database. Existing fixture preferences are retained between runs.

From the repository root, publish to a fresh output directory:

```powershell
$captureTargets = Join-Path $PWD 'scripts\docs-capture\Capture.targets'
dotnet publish Ui/Ui.csproj -c Release -r win-x64 --self-contained true -m:1 `
  -p:BuildInParallel=false -p:SkipDeveloperBuildHooks=true `
  "-p:CustomAfterMicrosoftCommonTargets=$captureTargets" -o artifacts/docs-demo
```

Launch `artifacts/docs-demo/1Remote.exe`, then use Computer Use to navigate the real application UI. Once the desired view is visible, write its image name to `artifacts/docs-demo/capture-request.txt`. The fixture exports a native-resolution PNG and dimensions under `artifacts/docs-demo/png/`.

Supported names select the visible main window by default, or the appropriate settings/editor/launcher/about window for the existing documentation filenames. Menu windows use their WPF visuals with premultiplied alpha, composed over the application; the transparent launcher and about windows retain alpha. Ordinary native windows exclude the measured two-pixel left/right/bottom black frame by cropping three pixels on those sides. The pointer and Computer Use's separate click highlight are excluded.

For README screenshots, use light/list and dark/card views. Always choose a full theme preset through the application when switching palettes. The fixture includes bounded dialog checks and exports their results as `dialog-checks.json`. Stop the fixture and release Computer Use immediately after capture.

These sources are only compiled when the explicit targets file above is supplied. A normal publish continues to use `_1RM.Program` and contains no fixture entry point.
