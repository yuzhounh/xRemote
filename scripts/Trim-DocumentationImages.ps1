param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-BlackPixel($Image, [int]$X, [int]$Y) {
    $pixel = $Image.GetPixel($X, $Y)
    return $pixel.A -eq 255 -and $pixel.R -eq 0 -and $pixel.G -eq 0 -and $pixel.B -eq 0
}

# Only trim the measured two-pixel frame. Already corrected or transparent
# images are skipped, so running this again cannot progressively crop content.
$files = @(Get-ChildItem (Join-Path $ProjectRoot 'docs/images') -Filter '*.png') +
         @(Get-ChildItem (Join-Path $ProjectRoot 'assets/screenshots') -Filter '*.png')
foreach ($file in $files) {
    $image = [Drawing.Bitmap]::new($file.FullName)
    $trim = $true
    foreach ($fraction in @(0.2, 0.5, 0.8)) {
        $x = [int]($image.Width * $fraction); $y = [int]($image.Height * $fraction)
        foreach ($depth in @(0, 1)) {
            $trim = $trim -and (Test-BlackPixel $image $depth $y) -and
                (Test-BlackPixel $image ($image.Width - 1 - $depth) $y) -and
                (Test-BlackPixel $image $x ($image.Height - 1 - $depth))
        }
        $trim = $trim -and !(Test-BlackPixel $image 2 $y) -and
            !(Test-BlackPixel $image ($image.Width - 3) $y) -and
            !(Test-BlackPixel $image $x ($image.Height - 3))
    }
    if (!$trim) { $image.Dispose(); continue }
    $rect = [Drawing.Rectangle]::new(3, 0, $image.Width - 6, $image.Height - 3)
    $cropped = $image.Clone($rect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $image.Dispose()
    try { $cropped.Save($file.FullName, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $cropped.Dispose() }
    Write-Output "Trimmed $($file.Name): left/right/bottom 3px"
}
