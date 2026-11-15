# HD - 바탕화면 디데이 위젯 (PowerShell + WinForms, Python 불필요)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing, Microsoft.VisualBasic
[System.Windows.Forms.Application]::EnableVisualStyles()

$Dir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$Data = Join-Path $env:APPDATA "HD\data.json"
New-Item -ItemType Directory -Force (Split-Path $Data) | Out-Null

function Load-Data {
    $d = @{ events = @(); selected = 0; x = 80; y = 80; top = $false }
    if (Test-Path $Data) {
        try {
            $j = Get-Content $Data -Raw -Encoding UTF8 | ConvertFrom-Json
            $d.events   = @($j.events | ForEach-Object { @{ name = [string]$_.name; date = [string]$_.date } })
            $d.selected = [int]$j.selected; $d.x = [int]$j.x; $d.y = [int]$j.y; $d.top = [bool]$j.top
        } catch {}
    }
    return $d
}
function Save-Data { ConvertTo-Json $script:d -Depth 5 | Set-Content $Data -Encoding UTF8 }
$script:d = Load-Data

$Red   = [Drawing.Color]::FromArgb(230, 0, 18)
$Black = [Drawing.Color]::FromArgb(11, 11, 11)

function Get-DDay($dateStr) {
    $n = [int](([datetime]::Parse($dateStr).Date - (Get-Date).Date).TotalDays)
    if ($n -eq 0) { return @("D-Day", 0) }
    if ($n -gt 0) { return @("D-$n", $n) }
    return @("D+$(-$n)", $n)
}

# ---- 창 ----
$f = New-Object Windows.Forms.Form
$f.FormBorderStyle = 'None'; $f.StartPosition = 'Manual'
$f.Location = New-Object Drawing.Point($d.x, $d.y)
$f.BackColor = $Black; $f.ShowInTaskbar = $false; $f.TopMost = $d.top
$f.Size = New-Object Drawing.Size(300, 120)
$f.Add_Paint({ param($s, $e)
    $pen = New-Object Drawing.Pen($Red, 4)
    $e.Graphics.DrawRectangle($pen, 2, 2, $s.Width - 5, $s.Height - 5) })

$pic = New-Object Windows.Forms.PictureBox
$pic.Size = '84,84'; $pic.Location = '12,18'; $pic.SizeMode = 'Zoom'
$png = Join-Path $Dir "icon.png"
if (Test-Path $png) { $pic.Image = [Drawing.Image]::FromFile($png) }
$name = New-Object Windows.Forms.Label
$name.Location = '104,12'; $name.Size = '186,26'; $name.ForeColor = 'White'
$name.Font = New-Object Drawing.Font("맑은 고딕", 12, [Drawing.FontStyle]::Bold)
$big = New-Object Windows.Forms.Label
$big.Location = '104,38'; $big.Size = '190,56'; $big.ForeColor = $Red
$big.Font = New-Object Drawing.Font("Impact", 30)
$sub = New-Object Windows.Forms.Label
$sub.Location = '104,94'; $sub.Size = '186,18'; $sub.ForeColor = 'Gray'
$sub.Font = New-Object Drawing.Font("맑은 고딕", 9)
foreach ($c in $pic, $name, $big, $sub) { $c.BackColor = $Black; $f.Controls.Add($c) }

function Current { if ($d.events.Count -eq 0) { return $null }
    $d.selected = $d.selected % $d.events.Count; return $d.events[$d.selected] }

function Refresh-View {
    $e = Current
    if (-not $e) { $name.Text = "우클릭 → 디데이 추가"; $big.Text = "HD"; $big.ForeColor = $Red; $sub.Text = ""; return }
    $r = Get-DDay $e.date
    $name.Text = $e.name; $big.Text = $r[0]; $sub.Text = $e.date
    $big.ForeColor = $(if ($r[1] -ge 0) { $Red } else { [Drawing.Color]::Gray })
}

function Ask-Date($init) {
    $s = [Microsoft.VisualBasic.Interaction]::InputBox("날짜 (YYYY-MM-DD)", "HD", $init)
    if (-not $s) { return $null }
    $out = [datetime]::MinValue
    if ([datetime]::TryParse($s.Trim(), [ref]$out)) { return $out.ToString("yyyy-MM-dd") }
    [Windows.Forms.MessageBox]::Show("날짜 형식이 틀렸어요. 예: 2026-12-25", "HD") | Out-Null
    return $null
}

# ---- 메뉴 ----
$menu = New-Object Windows.Forms.ContextMenuStrip
function Build-Menu {
    $menu.Items.Clear()
    $menu.Items.Add("디데이 추가", $null, {
        $n = [Microsoft.VisualBasic.Interaction]::InputBox("디데이 이름", "HD", "")
        if (-not $n) { return }
        $dt = Ask-Date ""
        if ($dt) { $script:d.events += @{ name = $n; date = $dt }
            $script:d.selected = $script:d.events.Count - 1; Save-Data; Refresh-View } }) | Out-Null
    $menu.Items.Add("수정", $null, {
        $e = Current; if (-not $e) { return }
        $n = [Microsoft.VisualBasic.Interaction]::InputBox("디데이 이름", "HD", $e.name)
        if (-not $n) { return }
        $dt = Ask-Date $e.date
        if ($dt) { $e.name = $n; $e.date = $dt; Save-Data; Refresh-View } }) | Out-Null
    $menu.Items.Add("삭제", $null, {
        $e = Current; if (-not $e) { return }
        if ([Windows.Forms.MessageBox]::Show("'$($e.name)' 삭제할까요?", "HD", 'YesNo') -eq 'Yes') {
            $script:d.events = @($script:d.events | Where-Object { $_ -ne $e }); Save-Data; Refresh-View } }) | Out-Null
    if ($d.events.Count -gt 1) {
        $menu.Items.Add("-") | Out-Null
        for ($i = 0; $i -lt $d.events.Count; $i++) {
            $idx = $i; $ev = $d.events[$i]
            $mi = $menu.Items.Add("$($ev.name) ($((Get-DDay $ev.date)[0]))")
            $mi.Tag = $idx
            $mi.Add_Click({ param($s, $a) $script:d.selected = [int]$s.Tag; Save-Data; Refresh-View })
        }
    }
    $menu.Items.Add("-") | Out-Null
    $top = $menu.Items.Add("항상 위에 표시"); $top.CheckOnClick = $true; $top.Checked = $d.top
    $top.Add_Click({ $script:d.top = $this.Checked; $f.TopMost = $this.Checked; Save-Data })
    $menu.Items.Add("종료", $null, { $f.Close() }) | Out-Null
}
$menu.Add_Opening({ Build-Menu })

# ---- 이동 / 클릭 ----
$script:drag = $null
foreach ($c in $f, $pic, $name, $big, $sub) {
    $c.ContextMenuStrip = $menu
    $c.Add_MouseDown({ param($s, $e) if ($e.Button -eq 'Left') { $script:drag = [Windows.Forms.Cursor]::Position
        $script:org = $f.Location } })
    $c.Add_MouseMove({ if ($script:drag -and [Windows.Forms.Control]::MouseButtons -eq 'Left') {
        $p = [Windows.Forms.Cursor]::Position
        $f.Location = New-Object Drawing.Point(($script:org.X + $p.X - $script:drag.X), ($script:org.Y + $p.Y - $script:drag.Y)) } })
    $c.Add_MouseUp({ if ($script:drag) { $script:drag = $null
        $script:d.x = $f.Location.X; $script:d.y = $f.Location.Y; Save-Data } })
    $c.Add_DoubleClick({ if ($d.events.Count -gt 1) { $script:d.selected = ($d.selected + 1) % $d.events.Count; Save-Data; Refresh-View } })
}

$timer = New-Object Windows.Forms.Timer; $timer.Interval = 60000
$timer.Add_Tick({ Refresh-View }); $timer.Start()
Refresh-View
[Windows.Forms.Application]::Run($f)
