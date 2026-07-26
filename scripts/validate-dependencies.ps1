[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projects = Get-ChildItem -Path (Join-Path $repositoryRoot 'src'), (Join-Path $repositoryRoot 'tests') -Recurse -Filter '*.csproj'
$violations = [System.Collections.Generic.List[string]]::new()

foreach ($projectFile in $projects) {
    [xml]$project = Get-Content -Raw -LiteralPath $projectFile.FullName
    $packageReferences = @($project.Project.ItemGroup.PackageReference | Where-Object { $_.Include })
    $projectReferences = @($project.Project.ItemGroup.ProjectReference | Where-Object { $_.Include })
    $projectName = $projectFile.BaseName

    if ($projectName -match '\.Domain$' -and $packageReferences.Count -gt 0) {
        $packages = ($packageReferences | ForEach-Object { $_.Include }) -join ', '
        $violations.Add("${projectName}: Domain projects must not reference packages ($packages).")
    }

    if ($projectName -match '\.Domain$') {
        foreach ($reference in $projectReferences) {
            $referencePath = [string]$reference.Include
            if ($referencePath -match '(?i)(Infrastructure|Api|\.Application)') {
                $violations.Add("${projectName}: Domain project references an outer layer ($referencePath).")
            }
        }
    }

    if ($projectName -match '(?i)\.Domain$' -and $projectName -notmatch '(?i)TradeMind\.') {
        $violations.Add("${projectName}: project naming does not use the TradeMind module prefix.")
    }

    if ($projectName -eq 'TradeMind.Api') {
        foreach ($reference in $projectReferences) {
            $referencePath = [string]$reference.Include
            if ($referencePath -match '(?i)\.Domain\.csproj$') {
                $violations.Add("${projectName}: API project must reference application contracts, not domain projects directly ($referencePath).")
            }
            if ($referencePath -match '(?i)(Broker|MT5|MetaTrader|OpenAI)') {
                $violations.Add("${projectName}: API project must not reference broker, MT5 or concrete LLM projects ($referencePath).")
            }
        }

        $endpointFiles = Get-ChildItem -Path (Join-Path $projectFile.DirectoryName 'Endpoints') -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue
        foreach ($endpointFile in $endpointFiles) {
            $endpointText = Get-Content -Raw -LiteralPath $endpointFile.FullName
            if ($endpointText -match '(?i)TradeMind\.[^\r\n]*(Infrastructure|\.Domain)') {
                $violations.Add("${projectName}: endpoint code must not depend on infrastructure or domain implementation ($($endpointFile.Name)).")
            }
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Dependency rules passed for $($projects.Count) projects."
