param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
[xml]$svg = Get-Content -LiteralPath (Join-Path $ProjectRoot 'assets\branding\xremote.svg') -Raw
$brushes = @{}
foreach ($gradient in $svg.SelectNodes('//*[local-name()="linearGradient"]')) {
    $brush = [System.Windows.Media.LinearGradientBrush]::new()
    $brush.StartPoint = [System.Windows.Point]::new([double]$gradient.x1, [double]$gradient.y1)
    $brush.EndPoint = [System.Windows.Point]::new([double]$gradient.x2, [double]$gradient.y2)
    foreach ($stop in $gradient.ChildNodes) {
        $brush.GradientStops.Add([System.Windows.Media.GradientStop]::new(
            [System.Windows.Media.ColorConverter]::ConvertFromString($stop.GetAttribute('stop-color')), [double]$stop.offset))
    }
    $brushes[$gradient.id] = $brush
}
$frames = @{}
foreach ($size in @(16,20,24,32,40,48,64,96,128,256,400)) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $dc.PushTransform([System.Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))
    foreach ($shape in $svg.DocumentElement.ChildNodes) {
        if ($shape.LocalName -eq 'rect') {
            $id = $shape.fill -replace '^url\(#|\)$', ''
            $dc.DrawRoundedRectangle($brushes[$id], $null,
                [System.Windows.Rect]::new([double]$shape.x,[double]$shape.y,[double]$shape.width,[double]$shape.height),
                [double]$shape.rx, [double]$shape.rx)
        } elseif ($shape.LocalName -eq 'path') {
            $pen = [System.Windows.Media.Pen]::new([System.Windows.Media.Brushes]::White, [double]$shape.GetAttribute('stroke-width'))
            $pen.StartLineCap = $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
            $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
            $dc.DrawGeometry($null, $pen, [System.Windows.Media.Geometry]::Parse($shape.d))
        }
    }
    $dc.Pop()
    $dc.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream)
    $frames[$size] = $stream.ToArray()
    $stream.Dispose()
    if ($size -in @(32,64,256,400)) {
        [System.IO.File]::WriteAllBytes((Join-Path $ProjectRoot "Ui\Resources\Image\Logo\logo$size.png"), $frames[$size])
    }
}
# Each ICO directory entry contains a PNG frame; Windows chooses the correct DPI size.
$iconSizes = @(16,20,24,32,40,48,64,96,128,256)
$iconStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($iconStream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconSizes.Count)
$offset = 6 + 16 * $iconSizes.Count
foreach ($size in $iconSizes) {
    $dimension = if ($size -eq 256) { 0 } else { $size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frames[$size].Length); $writer.Write([uint32]$offset)
    $offset += $frames[$size].Length
}
foreach ($size in $iconSizes) { $writer.Write([byte[]]$frames[$size]) }
foreach ($name in @('LOGO.ico','LOGO_D.ico')) {
    [System.IO.File]::WriteAllBytes((Join-Path $ProjectRoot "Ui\$name"), $iconStream.ToArray())
}
$writer.Dispose(); $iconStream.Dispose()
Write-Output 'Exported four PNG sizes and Release/Debug ICOs (10 DPI sizes) from assets/branding/xremote.svg.'
