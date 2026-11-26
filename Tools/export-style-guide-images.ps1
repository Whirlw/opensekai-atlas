# Turns a `Tools/capture-screens.ps1 -StyleGuide` run into the pictures of docs/reference/ui-style-guide.md:
# picks the frames the document shows, cuts the close-ups, scales them down and writes JPEGs to docs/images/ui-style.
#
#   powershell -File Tools/export-style-guide-images.ps1 -Source TestResults/screenshots/style-guide
#
# The run has to come from a sample workspace (Tools/new-sample-workspace.ps1): a player's own library shows
# other people's covers, which must not go into the repository. Verify the required PNGs against the sample run's report.
#
# Crops are in pixels of a 2800x1272 frame. A picture is never scaled up.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,
    [string]$Destination,
    [int]$Width = 1600,
    [int]$Quality = 82
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Destination) { $Destination = Join-Path $root 'docs/images/ui-style' }
if (-not [IO.Path]::IsPathRooted($Source)) { $Source = Join-Path $root $Source }
. (Join-Path $PSScriptRoot 'style-guide-frames.ps1')

# name in docs = frame[, crop x, y, width, height]
$images = [ordered]@{
    # splash and home
    'splash-fall'             = 'intro_000.950s'
    'splash-hit'              = 'intro_001.120s'
    'splash-wordmark'         = 'intro_002.600s'
    'home'                    = 'home'
    'home-player'             = 'medals-home2'
    'home-swipe'              = 'profile-swipe'
    'home-handoff'            = 'server_000.350s'
    'about'                   = 'about'
    # library
    'library-list'            = 'library-list'
    'library-grid'            = 'library-grid'
    'library-grouped'         = 'library-grouped'
    'library-accent'          = 'library-accent'
    'library-missing'         = 'library-missing'
    'library-empty'           = 'library-empty'
    'library-details-ranked'  = 'library-details-ranked'
    'library-details-unranked' = 'library-details-unranked'
    'library-rules'           = 'library-rules'
    'actions'                 = 'actions'
    'settings'                = 'library-settings'
    'settings-trace'          = 'settings-trace'
    'loading'                 = 'loading'
    'music-info'              = 'info_001.500s'
    # result
    'result'                  = 'result-default'
    'result-full-combo'       = 'result-fc'
    'result-all-perfect'      = 'result-ap'
    'result-failed'           = 'result-failed'
    'result-auto'             = 'result-auto'
    'result-save-failed'      = 'result-savefailed'
    'result-load-error'       = 'result-loaderror'
    # server
    'server-empty'            = 'sample-servers-empty'
    'server-list'             = 'server-list'
    'server-add'              = 'server-add'
    'server-sections'         = 'server-sonolus-home'
    'server-search'           = 'server-sonolus-search'
    'server-all'              = 'server-sonolus-all'
    'server-level'            = 'server-level'
    'server-level-community'  = 'server-level-community'
    'server-level-leaderboard' = 'server-level-leaderboard'
    # resource packs
    'packs-overview'          = 'packs-overview'
    'packs-detail'            = 'packs-detail'
    'packs-preview'           = 'packs-preview'
    'packs-category'          = 'packs-category-audio'
    # profile
    'profile-best30'          = 'profile-best30'
    'profile-history'         = 'profile-history'
    'profile-edit'            = 'profile-edit'
    'profile-import'          = 'profile-import'
    'profile-medals'          = 'medals-picker1'
    # dan
    'dan'                     = 'dan-passed'
    'dan-editor'              = 'dan-editor'
    'dan-grades'              = 'dan-grades'
    'dan-paste'               = 'dan-paste'
    'dan-paste-error'         = 'dan-paste-error'
    'dan-delete'              = 'dan-delete'
    'dan-prepare'             = 'dan-prepare'
    'dan-rest'                = 'dan-rest'
    'dan-result-pass'         = 'dan-result-pass'
    'dan-award'               = 'dan_000.750s-award'
    'dan-result-fail'         = 'dan-result-fail'
    # achievements
    'achievements'            = 'achievements-all'
    'achievements-special'    = 'achievements-special'
    'achievements-reward'     = 'achievements-reward-FC-02'
    'achievements-claim'      = 'achievements-claim50'
    'achievements-popup'      = 'achievements-unlock-multi'
    # badges and medals
    'ranking-badges'          = 'ranking-badges'
    'ranking-upgrade'         = 'ranking-upgrade-0.30'
    'ranking-gallery'         = 'ranking-gallery'
    'medal-tiers'             = 'dan-medal-tiers'
    'medal-series'            = 'medals-series'
    'medal-special'           = 'medals-special120'
    'avatars'                 = 'medals-avatars'
    # close-ups
    'part-topbar'             = 'library-list', 60, 10, 2680, 140
    'part-rows'               = 'library-list', 70, 155, 1310, 430
    'part-tiles'              = 'library-grid', 70, 155, 1310, 755
    'part-record'             = 'library-list', 1440, 690, 1290, 385
    'part-buttons'            = 'library-list', 1440, 1090, 1290, 145
    'part-dropdown'           = 'library-dropdown', 400, 20, 1000, 660
    'part-settings'           = 'library-settings', 530, 150, 900, 1000
    'part-armed'              = 'actions-armed', 880, 350, 1040, 560
    'part-hint'               = 'library-hint', 1980, 150, 750, 190
    'part-tabs'               = 'server-level', 700, 590, 1300, 110
    'part-chips'              = 'server-level', 700, 160, 1300, 75
}

$files = foreach ($entry in $images.Values) { @($entry)[0] + '.png' }
Use-StyleGuideFrames $Source $files {
    param($streams)
    Add-Type -AssemblyName System.Drawing
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $options = New-Object System.Drawing.Imaging.EncoderParameters 1
    $options.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]$Quality)
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    $total = 0
    foreach ($name in $images.Keys) {
        $entry = @($images[$name])
        $stream = $streams[$entry[0] + '.png']
        $stream.Position = 0
        $frame = [System.Drawing.Image]::FromStream($stream)
        try {
            $area = if ($entry.Count -eq 5) { New-Object System.Drawing.Rectangle $entry[1], $entry[2], $entry[3], $entry[4] }
                    else { New-Object System.Drawing.Rectangle 0, 0, $frame.Width, $frame.Height }
            $outWidth = [Math]::Min($Width, $area.Width)
            $outHeight = [int][Math]::Round($area.Height * $outWidth / $area.Width)
            $bitmap = New-Object System.Drawing.Bitmap $outWidth, $outHeight, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $target = New-Object System.Drawing.Rectangle 0, 0, $outWidth, $outHeight
                $graphics.DrawImage($frame, $target, $area, [System.Drawing.GraphicsUnit]::Pixel)
                $path = Join-Path $Destination ($name + '.jpg')
                $bitmap.Save($path, $codec, $options)
                $total += (Get-Item -LiteralPath $path).Length
            } finally { $graphics.Dispose(); $bitmap.Dispose() }
        } finally { $frame.Dispose() }
    }

    Write-Host ("[style-guide] {0} pictures, {1:N1} MB -> {2}" -f $images.Count, ($total / 1MB), $Destination)
}
