param([switch]$History)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$git = Get-Command git -ErrorAction SilentlyContinue
if (-not $git) { $git = Get-Item (Join-Path $env:ProgramFiles 'Git\cmd\git.exe') }
Push-Location $root
try {
    $patterns = @(
        '(?i)(gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-[A-Za-z0-9]{25,})',
        '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----',
        [Regex]::Escape($env:USERPROFILE),
        [Regex]::Escape($env:USERPROFILE.Replace('\','\\'))
        [Regex]::Escape($root)
        [Regex]::Escape($root.Replace('\','\\'))
    )
    if ($env:USERNAME.Length -ge 4) { $patterns += '(?i)\b' + [Regex]::Escape($env:USERNAME) + '\b' }
    [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
    $files = @(& $git -c core.quotepath=false ls-files --cached --others --exclude-standard) | Sort-Object -Unique
    $findings = @()
    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        $bytes = [IO.File]::ReadAllBytes((Join-Path $root $file))
        foreach ($encoding in @([Text.Encoding]::UTF8,[Text.Encoding]::Unicode)) {
            $text = $encoding.GetString($bytes)
            foreach ($pattern in $patterns) { if ($text -match $pattern) { $findings += $file; break } }
        }
    }
    if ($History) {
        foreach ($revision in @(& $git rev-list --all)) {
            $text = (& $git show --format=fuller --no-ext-diff $revision | Out-String)
            foreach ($pattern in $patterns) { if ($text -match $pattern) { $findings += "history:$revision"; break } }
        }
    }
    if ($findings.Count) { $findings | Sort-Object -Unique; throw 'Potential private data found. Review locally; do not print matched secrets.' }
    Write-Output "PASS: $($files.Count) source files and requested history scan. Also manually review screenshots, author emails and archive manifests."
} finally { Pop-Location }
