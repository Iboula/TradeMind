[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$ResultsDirectory = 'TestResults'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsPath = Join-Path $repositoryRoot $ResultsDirectory
$testProjects = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests') -Recurse -Filter '*.csproj' | Sort-Object FullName

if (Test-Path $resultsPath) {
    Remove-Item -Recurse -Force $resultsPath
}

New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null

foreach ($testProject in $testProjects) {
    $projectResultsPath = Join-Path $resultsPath $testProject.BaseName
    New-Item -ItemType Directory -Path $projectResultsPath -Force | Out-Null

    Write-Host "Testing $($testProject.BaseName)"
    & dotnet test $testProject.FullName `
        --configuration $Configuration `
        --no-build `
        --logger "trx;LogFileName=$($testProject.BaseName).trx" `
        --results-directory $projectResultsPath `
        '--collect:XPlat Code Coverage' `
        --verbosity minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test project failed: $($testProject.BaseName)"
        exit $LASTEXITCODE
    }
}

Write-Host "All $($testProjects.Count) test projects passed sequentially."
