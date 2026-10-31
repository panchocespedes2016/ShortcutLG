$ini = Join-Path $env:LOCALAPPDATA 'ShortcutLG\tray.ini'
Copy-Item -LiteralPath $ini -Destination ($ini + '.bak-20261031') -Force
$lines = Get-Content -LiteralPath $ini
$lines = $lines | ForEach-Object { if ($_ -match '^\s*ClientKey\s*=') { 'ClientKey=' } else { $_ } }
Set-Content -LiteralPath $ini -Value $lines -Encoding UTF8
Get-Content -LiteralPath $ini | ForEach-Object { if ($_ -match '^ClientKey=') { 'ClientKey=<vacia>' } else { $_ } }
