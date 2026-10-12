<#
.SYNOPSIS
    Re-records the admin-panel tutorial videos whose page or tutorial changed since the last recording.

.DESCRIPTION
    Starts the built site on IIS Express, logs in to the given store's admin panel with Microsoft Edge,
    plays each tutorial stored in the database (table Tutorials) and saves the result to
    Content\tutorials\videos\{Page}.webm. Each new video is recorded in the TutorialVideos table, and the tutorial
    tables are exported to App_Data\Seed\tutorials.sql so the next deployment carries them.
    Run it after building and before publishing; unchanged pages are skipped.
    The recorder only navigates and highlights, it never submits forms, so the store's data is left as is.

.EXAMPLE
    .\update-tutorials.ps1 -Password 'Admin@123'
    .\update-tutorials.ps1 -Password 'Admin@123' -Only Products,EditProduct -Force
#>
param(
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$Company = 'FASHION',
    [string]$User = 'admin',
    [int]$Port = 8095,
    [string[]]$Only,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'db.ps1')
$site = (Resolve-Path (Join-Path $PSScriptRoot '..\..\NikooSystem')).Path
$iisExpress = Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'
$connectionString = Get-NikooConnectionString $site

if (-not (Test-Path (Join-Path $site 'bin\NikooSystem.dll'))) { throw 'Build the NikooSystem project first.' }
Push-Location $PSScriptRoot
try {
    if (-not (Test-Path 'node_modules\playwright-core')) {
        npm install --no-fund --no-audit
        if ($LASTEXITCODE) { throw 'npm install failed.' }
    }
    # Playwright encodes the recordings with its own small ffmpeg build; the browser itself is the installed Edge.
    npx playwright-core install ffmpeg
    if ($LASTEXITCODE) { throw 'Installing the Playwright ffmpeg helper failed.' }
}
finally { Pop-Location }

$recorded = 0
$server = Start-Process $iisExpress -ArgumentList "/path:`"$site`"", "/port:$Port" -PassThru -WindowStyle Hidden
try {
    $ready = $false
    for ($i = 0; $i -lt 60 -and -not $ready; $i++) {
        try { (New-Object Net.Sockets.TcpClient('localhost', $Port)).Dispose(); $ready = $true } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ready) { throw "IIS Express did not start on port $Port." }

    $nodeArgs = @('record.mjs', '--base', "http://localhost:$Port", '--company', $Company, '--user', $User)
    if ($Force) { $nodeArgs += '--force' }
    if ($Only) { $nodeArgs += @('--only', ($Only -join ',')) }

    $env:TUTORIAL_PASSWORD = $Password
    Push-Location $PSScriptRoot
    try {
        node @nodeArgs | ForEach-Object {
            $parts = $_ -split "`t"
            if ($parts.Count -eq 5 -and $parts[0] -eq 'RECORDED') {
                Invoke-NikooQuery $connectionString @'
UPDATE [dbo].[TutorialVideos] SET [VideoFile] = @file, [Fingerprint] = @fingerprint, [RecordedAt] = @recordedAt WHERE [Page] = @page;
IF @@ROWCOUNT = 0 INSERT INTO [dbo].[TutorialVideos] ([Page], [VideoFile], [Fingerprint], [RecordedAt]) VALUES (@page, @file, @fingerprint, @recordedAt);
'@ @{ page = $parts[1]; file = $parts[2]; fingerprint = $parts[3]; recordedAt = [datetime]::Parse($parts[4], [Globalization.CultureInfo]::InvariantCulture, 'RoundtripKind') } | Out-Null
                $recorded++
            }
            else { $_ }
        }
        $exitCode = $LASTEXITCODE
    }
    finally { Pop-Location }
}
finally {
    Remove-Item Env:TUTORIAL_PASSWORD -ErrorAction SilentlyContinue
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
}

if ($recorded) { & (Join-Path $PSScriptRoot 'export-tutorials.ps1') -ConnectionString $connectionString }
exit $exitCode
