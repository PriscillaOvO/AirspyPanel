param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
    throw '.NET Framework compiler not found. Install .NET Framework 4.8.'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location (Join-Path $PSScriptRoot 'src')
try {
    & "$framework\csc.exe" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ "/out:$output\AirspyPanel.exe" /win32manifest:app.manifest /resource:Panel.xaml,AirspyPanel.Panel.xaml /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Xaml.dll /reference:System.Web.Extensions.dll "/reference:$framework\WPF\PresentationFramework.dll" "/reference:$framework\WPF\PresentationCore.dll" "/reference:$framework\WPF\WindowsBase.dll" Core.cs Monitoring.cs App.cs Tests.cs AssemblyInfo.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    Copy-Item -LiteralPath AirspyPanel.exe.config -Destination $output
} finally { Pop-Location }
Write-Output "Built $output\AirspyPanel.exe"
