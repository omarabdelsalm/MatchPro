$desktop = [Environment]::GetFolderPath('Desktop')
$wscript = New-Object -ComObject WScript.Shell
$targetExe = "E:\myItems\MatchAnalystPro\MatchAnalystPro.Shell\bin\Debug\net9.0-windows\MatchAnalystPro.Shell.exe"
$shortcutPath = Join-Path $desktop "Match Analyst Pro.lnk"

$shortcut = $wscript.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = [System.IO.Path]::GetDirectoryName($targetExe)
$shortcut.Description = "Match Analyst Pro"
$shortcut.Save()

Write-Host "Shortcut created successfully at: $shortcutPath"
