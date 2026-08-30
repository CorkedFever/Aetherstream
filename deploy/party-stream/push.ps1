<#
.SYNOPSIS
    Pushes a film to the party stream so the group can watch it together.

.DESCRIPTION
    Reads the publish credentials from the server over SSH each time, so they never sit on this
    machine in a file that can be committed or copied by accident.

    Re-encodes only when it has to. A file that is already H.264 + AAC is copied through, which
    costs almost no CPU; anything else is converted, which does.

.EXAMPLE
    .\push.ps1 -Input "D:\party\Spirited Away.mp4"

.EXAMPLE
    # Skip to 20 minutes in - the only way to seek, since viewers cannot.
    .\push.ps1 -Input "D:\party\film.mkv" -StartAt 00:20:00
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Alias('Input')]
    [string]$Source,

    [string]$StartAt,

    # Forces a re-encode even when the file looks playable as-is.
    [switch]$Reencode,

    [string]$Server = 'your.server.example',
    [string]$SshKey = "$env:USERPROFILE\.ssh\id_ed25519",
    [string]$WatchHost = 'party.example.com:8443'
)

$ErrorActionPreference = 'Stop'

foreach ($tool in 'ffmpeg', 'ffprobe', 'ssh') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is not on PATH."
    }
}

if (-not ($Source -match '^https?://') -and -not (Test-Path -LiteralPath $Source)) {
    throw "No such file: $Source"
}

Write-Host "Fetching publish credentials from $Server..." -ForegroundColor Cyan
$envText = & ssh -i $SshKey -o BatchMode=yes "root@$Server" 'cat /opt/tsukino/party-stream/party.env'
if ($LASTEXITCODE -ne 0) { throw "Could not read party.env from the server." }

$creds = @{}
foreach ($line in $envText -split "`n") {
    if ($line -match '^\s*([A-Z_]+)=(.*)$') { $creds[$Matches[1]] = $Matches[2].Trim() }
}
foreach ($k in 'PARTY_PATH', 'PARTY_PUBLISH_PASS', 'PARTY_SRT_PASSPHRASE') {
    if (-not $creds.ContainsKey($k)) { throw "party.env is missing $k - has setup.sh been run?" }
}

# Decide copy vs re-encode from what is actually in the file. Copying is close to free; re-encoding
# a 1080p film in real time is not, and it is worth knowing which is about to happen.
$vCodec = ''; $aCodec = ''
if (-not $Reencode -and -not ($Source -match '^https?://')) {
    $vCodec = (& ffprobe -v error -select_streams v:0 -show_entries stream=codec_name -of csv=p=0 -- "$Source") 2>$null
    $aCodec = (& ffprobe -v error -select_streams a:0 -show_entries stream=codec_name -of csv=p=0 -- "$Source") 2>$null
}

$canCopy = (-not $Reencode) -and ($vCodec -eq 'h264') -and ($aCodec -in @('aac', 'mp3'))

# HLS can only cut a segment on a keyframe. A source with a long GOP - libx264 defaults to about
# 8 seconds - cannot fill a 2-second segment, so the stream sits there "playing" and never delivers
# a frame. Checked over the first 12 seconds only, so this stays cheap on a feature-length file.
if ($canCopy) {
    $kf = (& ffprobe -v error -select_streams v:0 -skip_frame nokey -read_intervals '%+12' `
        -show_entries frame=pts_time -of csv=p=0 -- "$Source") 2>$null
    # csv=p=0 still emits a trailing comma on the first row, so trim before matching - otherwise
    # the very timestamp that anchors the interval is discarded and the check silently never fires.
    $times = @($kf | ForEach-Object { "$_".Trim().TrimEnd(',') } |
        Where-Object { $_ -match '^[0-9]+(\.[0-9]+)?$' } | ForEach-Object { [double]$_ })
    if ($times.Count -ge 2) {
        $gap = [math]::Round(($times[-1] - $times[0]) / ($times.Count - 1), 1)
        if ($gap -gt 4) {
            Write-Host "Keyframes are ~${gap}s apart; HLS segments are 2s." -ForegroundColor Yellow
            Write-Host "  Viewers would wait a long time for the first frame, or get nothing." -ForegroundColor Yellow
            Write-Host "  Re-encoding instead. Pre-encode with '-g 60' to avoid this." -ForegroundColor Yellow
            $canCopy = $false
        }
    }
}

if ($canCopy) {
    Write-Host "Source is $vCodec/$aCodec - copying streams, near-zero CPU." -ForegroundColor Green
    $codecArgs = @('-c', 'copy')
} else {
    $why = if ($Reencode) { 'forced' } else { "source is '$vCodec/$aCodec'" }
    Write-Host "Re-encoding ($why). This uses real CPU for the whole film." -ForegroundColor Yellow
    $codecArgs = @(
        '-c:v', 'libx264', '-preset', 'veryfast', '-b:v', '3M', '-pix_fmt', 'yuv420p',
        # A keyframe every 2 seconds, matching hlsSegmentDuration. Without this the muxer cannot
        # close a segment on time and playback never starts.
        '-g', '60', '-keyint_min', '60', '-sc_threshold', '0',
        '-c:a', 'aac', '-b:a', '160k', '-ac', '2'
    )
}

$target = 'srt://{0}:8890?streamid=publish:{1}:publisher:{2}&passphrase={3}&pkt_size=1316' -f `
    $Server, $creds.PARTY_PATH, $creds.PARTY_PUBLISH_PASS, $creds.PARTY_SRT_PASSPHRASE

$watchUrl = 'https://{0}/{1}/index.m3u8' -f $WatchHost, $creds.PARTY_PATH

Write-Host ''
Write-Host 'Share this with the room:' -ForegroundColor Cyan
Write-Host "  /aether play $watchUrl" -ForegroundColor White
Write-Host ''
Write-Host 'Ctrl+C stops the broadcast for everyone.' -ForegroundColor DarkGray
Write-Host ''

try { Set-Clipboard -Value "/aether play $watchUrl"; Write-Host '(command copied to clipboard)' -ForegroundColor DarkGray } catch { }

# -re paces the file at real time. Without it ffmpeg sends the whole film upstream as fast as the
# link allows and viewers get nothing usable.
$ffArgs = @('-hide_banner', '-loglevel', 'warning', '-stats', '-re')
if ($StartAt) { $ffArgs += @('-ss', $StartAt) }
$ffArgs += @('-i', $Source) + $codecArgs + @('-f', 'mpegts', $target)

& ffmpeg @ffArgs
