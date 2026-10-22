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
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION' }
$generated = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Path $generated -Force | Out-Null
$versionSource = Join-Path $generated 'Version.cs'
[IO.File]::WriteAllText($versionSource, ('[assembly:System.Reflection.AssemblyVersion("{0}.0")][assembly:System.Reflection.AssemblyFileVersion("{0}.0")]' -f $version))
Push-Location (Join-Path $PSScriptRoot 'src')
try {
    $resources = @(Get-ChildItem Resources/*.xml | ForEach-Object { '/resource:' + $_.FullName + ',AirspyPanel.Resources.' + $_.Name })
    $sources = @(Get-ChildItem *.cs | ForEach-Object FullName) + @($versionSource)
    & "$framework\csc.exe" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ "/out:$output\AirspyPanel.exe" /win32manifest:app.manifest /win32icon:Airplane.ico /resource:Airplane.xaml,AirspyPanel.Airplane.xaml /resource:Panel.xaml,AirspyPanel.Panel.xaml /resource:Airplane.ico,AirspyPanel.Airplane.ico /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Xaml.dll /reference:System.Web.Extensions.dll "/reference:$framework\WPF\PresentationFramework.dll" "/reference:$framework\WPF\PresentationCore.dll" "/reference:$framework\WPF\WindowsBase.dll" @resources @sources
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    Copy-Item -LiteralPath AirspyPanel.exe.config -Destination $output
} finally { Pop-Location }
Write-Output "Built $output\AirspyPanel.exe"
