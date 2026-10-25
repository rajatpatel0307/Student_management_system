param([switch]$Integration, [switch]$Publish)

$ErrorActionPreference = 'Stop'
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) { $PSNativeCommandUseErrorActionPreference = $false }
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    $resultDirectory = Join-Path $projectRoot '.build/quality'
    New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
    $results = [System.Collections.Generic.List[object]]::new()
    function Invoke-QualityCheck([string]$Name, [string]$Executable, [string[]]$CommandArguments) {
        Write-Host "Running $Name"
        $logPath = Join-Path $resultDirectory "$Name.log"
        $previousPreference = $ErrorActionPreference
        try {
            Get-Command -Name $Executable -CommandType Application -ErrorAction Stop | Out-Null
            # Windows PowerShell otherwise converts native stderr notices (such
            # as Git line-ending warnings) into terminating PowerShell errors.
            $ErrorActionPreference = 'Continue'
            & $Executable @CommandArguments > $logPath 2>&1
            $code = $LASTEXITCODE
        } catch { $_.Exception.GetType().Name | Set-Content -LiteralPath $logPath; $code = -1 }
        finally { $ErrorActionPreference = $previousPreference }
        $entry = [pscustomobject]@{ name = $Name; command = "$Executable $($CommandArguments -join ' ')"; exitCode = $code; status = $(if ($code -eq 0) { 'PASS' } else { 'FAIL' }); log = $logPath }
        $results.Add($entry)
        Write-Host "$Name : $($entry.status) (exit $code)"
    }
    Invoke-QualityCheck 'restore' 'dotnet' @('restore', 'Student_Management_System.sln')
    Invoke-QualityCheck 'debug-build' 'dotnet' @('build', 'Student_Management_System.sln', '--no-restore')
    Invoke-QualityCheck 'release-build' 'dotnet' @('build', 'Student_Management_System.sln', '--no-restore', '-c', 'Release')
    Invoke-QualityCheck 'backend-tests' 'dotnet' @('run', '--project', 'StaffTests/StaffTests.csproj', '--no-restore')
    Invoke-QualityCheck 'javascript-tests' 'node' @('--test', 'Tests/*.test.mjs')
    foreach ($scriptFile in @((Get-ChildItem -LiteralPath 'wwwroot/js' -Filter '*.js' -File)) + @(Get-Item -LiteralPath 'wwwroot/service-worker.js')) {
        Invoke-QualityCheck "syntax-$($scriptFile.BaseName)" 'node' @('--check', $scriptFile.FullName)
    }
    Invoke-QualityCheck 'dependency-advisories' 'dotnet' @('list', 'Student_Management_System.csproj', 'package', '--vulnerable', '--include-transitive', '--no-restore', '--format', 'json')
    $scan = $results | Where-Object name -eq 'dependency-advisories'
    if ($scan.exitCode -eq 0) {
        try {
            $advisories = Get-Content -LiteralPath $scan.log -Raw | ConvertFrom-Json
            if (-not $advisories.projects) { throw 'Missing project results' }
            $findings = @($advisories.projects | ForEach-Object { $_.frameworks } | ForEach-Object { @($_.topLevelPackages) + @($_.transitivePackages) } | Where-Object { $_.vulnerabilities.Count -gt 0 })
            if ($findings.Count -gt 0) { $scan.status = 'FAIL'; $scan.exitCode = 1 }
        } catch { $scan.status = 'FAIL'; $scan.exitCode = 1 }
    }
    if ($Integration) {
        Invoke-QualityCheck 'sql-https-razor-tests' 'dotnet' @('run', '--project', 'StaffTests/StaffTests.csproj', '--no-restore', '--', '--integration')
    } else {
        $results.Add([pscustomobject]@{ name = 'sql-https-razor-tests'; command = ''; exitCode = $null; status = 'NOT RUN'; log = '' })
    }
    Invoke-QualityCheck 'diff-check' 'git' @('diff', '--check')
    if ($Publish) {
        if (@($results | Where-Object { $_.status -eq 'FAIL' }).Count -eq 0) {
            Invoke-QualityCheck 'release-artifact' 'dotnet' @('publish', 'Student_Management_System.csproj', '--no-build', '--no-restore', '-c', 'Release', '-o', '.build/artifacts')
        } else {
            $results.Add([pscustomobject]@{ name = 'release-artifact'; command = ''; exitCode = $null; status = 'BLOCKED'; log = '' })
        }
    }
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resultDirectory 'results.json') -Encoding UTF8
    if (@($results | Where-Object { $_.status -eq 'FAIL' }).Count -gt 0) { exit 1 }
    exit 0
} finally { Pop-Location }
