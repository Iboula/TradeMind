[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projects = Get-ChildItem -Path (Join-Path $repositoryRoot 'src'), (Join-Path $repositoryRoot 'tests') -Recurse -Filter '*.csproj'
$violations = [System.Collections.Generic.List[string]]::new()

function Get-ReferencedProjectName {
    param([string]$ReferencePath)

    $normalizedPath = $ReferencePath.Replace('\', '/')
    return [System.IO.Path]::GetFileNameWithoutExtension($normalizedPath)
}

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

    if ($projectName -eq 'TradeMind.ExecutionSessions.Domain' -and $projectReferences.Count -gt 0) {
        $violations.Add("${projectName}: the persistence domain must not reference another project.")
    }

    if ($projectName -eq 'TradeMind.ExecutionSessions.Application') {
        foreach ($reference in $projectReferences) {
            $referencePath = [string]$reference.Include
            if ($referencePath -match '(?i)(Infrastructure|Api)') {
                $violations.Add("${projectName}: application code must not reference infrastructure or API projects ($referencePath).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(EntityFramework|Npgsql|AspNetCore)') {
                $violations.Add("${projectName}: application code must not reference database or HTTP packages ($($package.Include)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.ExecutionSessions.Infrastructure') {
        $allowedReferences = @('TradeMind.ExecutionSessions.Application', 'TradeMind.ExecutionSessions.Domain')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: infrastructure references an unexpected project ($referenceName).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Identity.Domain') {
        if ($projectReferences.Count -gt 0) {
            $references = ($projectReferences | ForEach-Object { $_.Include }) -join ', '
            $violations.Add("${projectName}: identity domain must not reference another project ($references).")
        }
    }

    if ($projectName -eq 'TradeMind.Identity.Application') {
        $allowedReferences = @('TradeMind.Identity.Domain')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: identity application references an unexpected project ($referenceName).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(AspNetCore|EntityFramework|Npgsql|JwtBearer|IdentityModel)') {
                $violations.Add("${projectName}: identity application must remain provider-neutral ($($package.Include)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Identity.Infrastructure') {
        $allowedReferences = @('TradeMind.Identity.Application', 'TradeMind.Identity.Domain')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: identity infrastructure references an unexpected project ($referenceName).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Observability.Abstractions') {
        if ($packageReferences.Count -gt 0) {
            $packages = ($packageReferences | ForEach-Object { $_.Include }) -join ', '
            $violations.Add("${projectName}: provider-neutral telemetry contracts must not reference packages ($packages).")
        }
        if ($projectReferences.Count -gt 0) {
            $references = ($projectReferences | ForEach-Object { $_.Include }) -join ', '
            $violations.Add("${projectName}: telemetry contracts must not reference another project ($references).")
        }
    }

    if ($projectName -eq 'TradeMind.Observability') {
        $allowedReferences = @('TradeMind.Observability.Abstractions')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: runtime observability references an unexpected project ($referenceName).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(Broker|MT5|OpenAI|Npgsql)') {
                $violations.Add("${projectName}: observability must not reference broker, MT5, LLM or provider-specific database packages ($($package.Include)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Brokers.Domain') {
        if ($packageReferences.Count -gt 0 -or $projectReferences.Count -gt 0) {
            $violations.Add("${projectName}: broker domain must remain dependent on BCL/shared primitives only.")
        }
    }

    if ($projectName -eq 'TradeMind.Brokers.Application') {
        $allowedReferences = @('TradeMind.Brokers.Domain', 'TradeMind.AI.RiskEngine.Domain', 'TradeMind.AI.TradingPlans.Domain', 'TradeMind.ExecutionSessions.Application', 'TradeMind.Observability.Abstractions')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: broker application references an unexpected project ($referenceName).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(EntityFramework|Npgsql|AspNetCore|BrokerSdk|MetaTrader|OpenAI)') {
                $violations.Add("${projectName}: broker application must remain provider-neutral ($($package.Include)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Brokers.Infrastructure') {
        $allowedReferences = @('TradeMind.Brokers.Application', 'TradeMind.Brokers.Domain')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: broker infrastructure references an unexpected project ($referenceName).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Brokers.MetaTrader5') {
        $allowedReferences = @('TradeMind.Brokers.Application', 'TradeMind.Brokers.Domain', 'TradeMind.Observability.Abstractions')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: MT5 adapter references an unexpected project ($referenceName).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(MetaTrader|MQL5|MT5|BrokerSdk)') {
                $violations.Add("${projectName}: MT5 adapter must not reference an external broker SDK ($($package.Include)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Brokers.MetaTrader5.Bridge.Contracts') {
        if ($packageReferences.Count -gt 0 -or $projectReferences.Count -gt 0) {
            $violations.Add("${projectName}: bridge contracts must remain transport and infrastructure neutral.")
        }
        $contractFiles = Get-ChildItem -Path $projectFile.DirectoryName -Recurse -Filter '*.cs'
        foreach ($contractFile in $contractFiles) {
            $contractText = Get-Content -Raw -LiteralPath $contractFile.FullName
            if ($contractText -match '(?i)(Microsoft\.AspNetCore|EntityFramework|Npgsql|TradeMind\.Brokers\.MetaTrader5(?!\.Bridge\.Contracts))') {
                $violations.Add("${projectName}: contract code references a forbidden infrastructure or broker type ($($contractFile.Name)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Brokers.MetaTrader5.Bridge.Client') {
        $allowedReferences = @('TradeMind.Brokers.MetaTrader5', 'TradeMind.Brokers.MetaTrader5.Bridge.Contracts', 'TradeMind.Observability.Abstractions')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: bridge client references an unexpected project ($referenceName).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(EntityFramework|Npgsql|AspNetCore|MetaTrader|MQL5|BrokerSdk)') {
                $violations.Add("${projectName}: bridge client must not reference database, ASP.NET or native MT5 packages ($($package.Include)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Brokers.MetaTrader5.Bridge.Host') {
        $allowedReferences = @('TradeMind.Brokers.MetaTrader5.Bridge.Contracts', 'TradeMind.Observability.Abstractions')
        foreach ($reference in $projectReferences) {
            $referenceName = Get-ReferencedProjectName ([string]$reference.Include)
            if ($referenceName -notin $allowedReferences) {
                $violations.Add("${projectName}: bridge host references an unexpected project ($referenceName).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(EntityFramework|Npgsql|MetaTrader|MQL5|BrokerSdk|OpenAI)') {
                $violations.Add("${projectName}: bridge host must not reference persistence, native MT5 or LLM packages ($($package.Include)).")
            }
        }
    }

    if ($projectName -notmatch '(?i)MetaTrader5') {
        foreach ($reference in $projectReferences) {
            if ([string]$reference.Include -match '(?i)(MetaTrader|MQL5|MT5)') {
                $violations.Add("${projectName}: MT5 dependencies must remain isolated inside the adapter project ($($reference.Include)).")
            }
        }
        foreach ($package in $packageReferences) {
            if ([string]$package.Include -match '(?i)(MetaTrader|MQL5|MT5)') {
                $violations.Add("${projectName}: MT5 packages must remain isolated inside the adapter project ($($package.Include)).")
            }
        }
    }

    if ($projectName -eq 'TradeMind.Api') {
        foreach ($reference in $projectReferences) {
            $referencePath = [string]$reference.Include
            if ($referencePath -match '(?i)\.Domain\.csproj$' -and $referencePath -notmatch '(?i)TradeMind\.Brokers\.Domain\.csproj$') {
                $violations.Add("${projectName}: API project must reference application contracts, not domain projects directly ($referencePath).")
            }
            if ($referencePath -match '(?i)(MT5|MetaTrader|OpenAI)') {
                $violations.Add("${projectName}: API project must not reference MT5 or concrete LLM projects ($referencePath).")
            }
        }

        $endpointFiles = Get-ChildItem -Path (Join-Path $projectFile.DirectoryName 'Endpoints') -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue
        foreach ($endpointFile in $endpointFiles) {
            $endpointText = Get-Content -Raw -LiteralPath $endpointFile.FullName
            if ($endpointText -match '(?i)TradeMind\.[^\r\n]*(Infrastructure|\.Domain)' -and $endpointText -notmatch '(?i)TradeMind\.Brokers\.Domain') {
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
