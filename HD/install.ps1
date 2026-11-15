# HD 설치: 파일 받기 + 바탕화면에 HD 바로가기(아이콘 포함) 만들기
$ErrorActionPreference = "Stop"
$base = "https://raw.githubusercontent.com/Quanous/mine/main/HD"
$dir  = Join-Path $env:APPDATA "HD\app"
New-Item -ItemType Directory -Force $dir | Out-Null
foreach ($n in "HD.ps1", "icon.png", "icon.ico") {
    Invoke-WebRequest "$base/$n" -OutFile (Join-Path $dir $n) -UseBasicParsing
}
$desktop = [Environment]::GetFolderPath("Desktop")
$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut((Join-Path $desktop "HD.lnk"))
$lnk.TargetPath = "powershell.exe"
$lnk.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$(Join-Path $dir 'HD.ps1')`""
$lnk.IconLocation = (Join-Path $dir "icon.ico")
$lnk.WorkingDirectory = $dir
$lnk.Save()
Write-Host "완료! 바탕화면의 HD를 더블클릭하세요."
Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$(Join-Path $dir 'HD.ps1')`""
