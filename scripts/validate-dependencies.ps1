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
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Dependency rules passed for $($projects.Count) projects."
