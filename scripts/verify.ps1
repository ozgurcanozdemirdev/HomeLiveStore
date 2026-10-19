param([int]$Port = 0, [switch]$BrowserChecks)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($Port -eq 0) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $Port = $listener.LocalEndpoint.Port
    $listener.Stop()
}
$runFolder = Join-Path $projectRoot ('App_Data/checks/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
Push-Location $projectRoot
try {
    dotnet build HomeLiveStore.slnx --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
    dotnet run --project tests/DomainChecks
    if ($LASTEXITCODE -ne 0) { throw 'Domain checks failed.' }
    $start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.ArgumentList.Add((Join-Path $projectRoot 'HomeLiveStore.Web/bin/Debug/net10.0/HomeLiveStore.dll'))
    $start.ArgumentList.Add('--urls')
    $start.ArgumentList.Add("http://127.0.0.1:$Port")
    $start.WorkingDirectory = Join-Path $projectRoot 'HomeLiveStore.Web'
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $start.Environment['ConnectionStrings__Store'] = 'Data Source=' + (Join-Path $runFolder 'store.db')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $server = [System.Diagnostics.Process]::Start($start)
    $stdout = $server.StandardOutput.ReadToEndAsync()
    $stderr = $server.StandardError.ReadToEndAsync()
    try {
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            if ($server.HasExited) { throw 'Web host exited before becoming ready.' }
            try {
                $response = Invoke-WebRequest "http://127.0.0.1:$Port/health" -TimeoutSec 1
                if ($response.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $ready) { throw 'Web host did not become ready.' }
        dotnet run --project tests/IntegrationChecks -- "http://127.0.0.1:$Port"
        if ($LASTEXITCODE -ne 0) { throw 'HTTP integration checks failed.' }
        if ($BrowserChecks) {
            node (Join-Path $projectRoot 'tests/BrowserChecks/regressions.cjs') "http://127.0.0.1:$Port" (Join-Path $runFolder 'browser')
            if ($LASTEXITCODE -ne 0) { throw 'Browser regression checks failed.' }
        }
    } finally {
        if (-not $server.HasExited) { $server.Kill($true) }
        $server.WaitForExit()
        [System.IO.File]::WriteAllText((Join-Path $runFolder 'web.log'), $stdout.GetAwaiter().GetResult())
        [System.IO.File]::WriteAllText((Join-Path $runFolder 'web-error.log'), $stderr.GetAwaiter().GetResult())
        $server.Dispose()
    }
    Write-Host "Verification passed. Isolated database and logs: $runFolder"
} finally {
    Pop-Location
}
