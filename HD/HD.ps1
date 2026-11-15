# HD - 디데이 리스트 (PowerShell + WinForms, Python 불필요)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

# 중복 실행 방지
$mutex = New-Object Threading.Mutex($false, "Local\HD_DDay_List")
if (-not $mutex.WaitOne(0)) { exit }

$Dir     = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root    = Join-Path $env:APPDATA "HD"
$ImgDir  = Join-Path $Root "images"
$Data    = Join-Path $Root "data.json"
New-Item -ItemType Directory -Force $ImgDir | Out-Null

$Red    = [Drawing.Color]::FromArgb(230, 0, 18)
$Gold   = [Drawing.Color]::FromArgb(255, 193, 7)
$Gray   = [Drawing.Color]::FromArgb(130, 130, 130)
$BgMain = [Drawing.Color]::FromArgb(18, 18, 18)
$BgRow  = [Drawing.Color]::FromArgb(32, 32, 32)
$Text   = [Drawing.Color]::White

# ---------- 데이터 ----------
function Load-Data {
    $d = @{ events = @(); x = 100; y = 100; w = 420; h = 520; top = $false }
    if (Test-Path $Data) {
        try {
            $j = Get-Content $Data -Raw -Encoding UTF8 | ConvertFrom-Json
            $d.events = @($j.events | ForEach-Object {
                @{ name = [string]$_.name; date = [string]$_.date; image = [string]$_.image } })
            foreach ($k in "x", "y", "w", "h") { if ($null -ne $j.$k) { $d[$k] = [int]$j.$k } }
            if ($null -ne $j.top) { $d.top = [bool]$j.top }
        } catch {}
    }
    return $d
}
function Save-Data { ConvertTo-Json $script:d -Depth 5 | Set-Content $Data -Encoding UTF8 }
$script:d = Load-Data

function Get-Days($e) { return [int](([datetime]::Parse($e.date).Date - (Get-Date).Date).TotalDays) }
function Format-Day($n) {
    if ($n -eq 0) { return "D-Day" }
    if ($n -gt 0) { return "D-$n" }
    return "D+$(-$n)"
}
function Load-Img($path) {   # 파일 잠금 없이 읽기
    try {
        $bytes = [IO.File]::ReadAllBytes($path)
        return [Drawing.Image]::FromStream((New-Object IO.MemoryStream(, $bytes)))
    } catch { return $null }
}
$DefaultImg = $null
$defPath = Join-Path $Dir "icon.png"
if (Test-Path $defPath) { $DefaultImg = Load-Img $defPath }

# ---------- 추가/수정 창 ----------
function Edit-Event($ev) {
    $dlg = New-Object Windows.Forms.Form
    $dlg.Text = $(if ($ev) { "디데이 수정" } else { "디데이 추가" })
    $dlg.FormBorderStyle = 'FixedDialog'; $dlg.MaximizeBox = $false; $dlg.MinimizeBox = $false
    $dlg.StartPosition = 'CenterParent'; $dlg.ClientSize = New-Object Drawing.Size(380, 250)
    $dlg.Font = New-Object Drawing.Font("맑은 고딕", 9.5)

    $l1 = New-Object Windows.Forms.Label; $l1.Text = "이름"; $l1.Location = '16,18'; $l1.AutoSize = $true
    $tb = New-Object Windows.Forms.TextBox; $tb.Location = '90,14'; $tb.Width = 270
    $l2 = New-Object Windows.Forms.Label; $l2.Text = "날짜"; $l2.Location = '16,58'; $l2.AutoSize = $true
    $dp = New-Object Windows.Forms.DateTimePicker; $dp.Location = '90,54'; $dp.Width = 150
    $dp.Format = 'Custom'; $dp.CustomFormat = 'yyyy-MM-dd'
    $l3 = New-Object Windows.Forms.Label; $l3.Text = "이미지"; $l3.Location = '16,100'; $l3.AutoSize = $true
    $pb = New-Object Windows.Forms.PictureBox; $pb.Location = '90,94'; $pb.Size = '80,80'
    $pb.SizeMode = 'Zoom'; $pb.BorderStyle = 'FixedSingle'
    $bImg = New-Object Windows.Forms.Button; $bImg.Text = "이미지 선택..."; $bImg.Location = '184,94'; $bImg.Size = '120,30'
    $bNo  = New-Object Windows.Forms.Button; $bNo.Text = "이미지 제거"; $bNo.Location = '184,130'; $bNo.Size = '120,30'
    $ok = New-Object Windows.Forms.Button; $ok.Text = "확인"; $ok.Location = '196,200'; $ok.Size = '80,30'
    $cc = New-Object Windows.Forms.Button; $cc.Text = "취소"; $cc.Location = '284,200'; $cc.Size = '80,30'
    $cc.DialogResult = 'Cancel'
    $dlg.Controls.AddRange(@($l1, $tb, $l2, $dp, $l3, $pb, $bImg, $bNo, $ok, $cc))
    $dlg.CancelButton = $cc

    $st = @{ img = ""; newPath = $null }
    if ($ev) {
        $tb.Text = $ev.name; $dp.Value = [datetime]::Parse($ev.date); $st.img = $ev.image
        if ($ev.image) { $p = Join-Path $ImgDir $ev.image; if (Test-Path $p) { $pb.Image = Load-Img $p } }
    }
    $bImg.Add_Click({
        $ofd = New-Object Windows.Forms.OpenFileDialog
        $ofd.Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico"
        if ($ofd.ShowDialog() -eq 'OK') { $st.newPath = $ofd.FileName; $pb.Image = Load-Img $ofd.FileName }
    })
    $bNo.Add_Click({ $st.newPath = $null; $st.img = ""; $pb.Image = $null })
    $ok.Add_Click({
        if (-not $tb.Text.Trim()) { [Windows.Forms.MessageBox]::Show("이름을 입력하세요.", "HD") | Out-Null; return }
        $dlg.DialogResult = 'OK'
    })

    $result = $null
    if ($dlg.ShowDialog($script:form) -eq 'OK') {
        $img = $st.img
        if ($st.newPath) {
            $img = [guid]::NewGuid().ToString("N") + [IO.Path]::GetExtension($st.newPath)
            Copy-Item $st.newPath (Join-Path $ImgDir $img)
        }
        $result = @{ name = $tb.Text.Trim(); date = $dp.Value.ToString("yyyy-MM-dd"); image = $img }
    }
    $dlg.Dispose()
    return $result
}

# ---------- 메인 창 ----------
$form = New-Object Windows.Forms.Form
$script:form = $form
$form.Text = "HD"
$form.StartPosition = 'Manual'
$form.Size = New-Object Drawing.Size($d.w, $d.h)
$form.MinimumSize = New-Object Drawing.Size(320, 200)
$form.BackColor = $BgMain
$form.TopMost = $d.top
$form.Location = New-Object Drawing.Point($d.x, $d.y)
$inScreen = $false
foreach ($s in [Windows.Forms.Screen]::AllScreens) { if ($s.WorkingArea.Contains($form.Location)) { $inScreen = $true } }
if (-not $inScreen) { $form.Location = New-Object Drawing.Point(100, 100) }
$ico = Join-Path $Dir "icon.ico"
if (Test-Path $ico) { try { $form.Icon = New-Object Drawing.Icon($ico) } catch {} }

$list = New-Object Windows.Forms.Panel
$list.Dock = 'Fill'; $list.AutoScroll = $true; $list.BackColor = $BgMain
$form.Controls.Add($list)

$menu = New-Object Windows.Forms.ContextMenuStrip
$script:target = $null

function Add-Row($e, $y) {
    $row = New-Object Windows.Forms.Panel
    $row.Location = New-Object Drawing.Point(6, $y); $row.Height = 68
    $row.Width = $list.ClientSize.Width - 12; $row.Anchor = 'Top,Left,Right'
    $row.BackColor = $BgRow; $row.Tag = $e

    $pic = New-Object Windows.Forms.PictureBox
    $pic.Location = '8,8'; $pic.Size = '52,52'; $pic.SizeMode = 'Zoom'; $pic.BackColor = $BgRow
    $img = $null
    if ($e.image) { $p = Join-Path $ImgDir $e.image; if (Test-Path $p) { $img = Load-Img $p } }
    if (-not $img) { $img = $DefaultImg }
    $pic.Image = $img

    $n = Get-Days $e
    $dl = New-Object Windows.Forms.Label
    $dl.Dock = 'Right'; $dl.Width = 120; $dl.TextAlign = 'MiddleRight'; $dl.BackColor = $BgRow
    $dl.Font = New-Object Drawing.Font("Segoe UI", 20, [Drawing.FontStyle]::Bold)
    $dl.Text = Format-Day $n
    $dl.ForeColor = $(if ($n -gt 0) { $Red } elseif ($n -eq 0) { $Gold } else { $Gray })

    $nl = New-Object Windows.Forms.Label
    $nl.Location = '68,10'; $nl.Size = New-Object Drawing.Size(([Math]::Max(60, $row.Width - 200)), 24)
    $nl.AutoEllipsis = $true; $nl.ForeColor = $Text; $nl.BackColor = $BgRow; $nl.Text = $e.name
    $nl.Font = New-Object Drawing.Font("맑은 고딕", 11, [Drawing.FontStyle]::Bold)
    $nl.Anchor = 'Top,Left,Right'
    $al = New-Object Windows.Forms.Label
    $al.Location = '68,38'; $al.Size = '150,20'; $al.ForeColor = $Gray; $al.BackColor = $BgRow
    $al.Font = New-Object Drawing.Font("맑은 고딕", 9); $al.Text = $e.date

    foreach ($c in $pic, $dl, $nl, $al) { $row.Controls.Add($c) }
    foreach ($c in $row, $pic, $dl, $nl, $al) {
        $c.Tag = $e; $c.ContextMenuStrip = $menu
        $c.Add_DoubleClick({ param($s, $a) $script:target = $s.Tag; Do-Edit })
    }
    $list.Controls.Add($row)
}

function Refresh-List {
    $list.SuspendLayout()
    $list.Controls.Clear()
    $sorted = @($script:d.events | Sort-Object { $n = Get-Days $_; if ($n -ge 0) { $n } else { 100000 - $n } })
    $y = 6
    foreach ($e in $sorted) { Add-Row $e $y; $y += 74 }
    if ($sorted.Count -eq 0) {
        $hint = New-Object Windows.Forms.Label
        $hint.Text = "빈 곳을 우클릭해서 디데이를 추가하세요"
        $hint.ForeColor = $Gray; $hint.AutoSize = $true; $hint.Location = '20,30'
        $hint.Font = New-Object Drawing.Font("맑은 고딕", 10)
        $hint.ContextMenuStrip = $menu
        $list.Controls.Add($hint)
    }
    $list.ResumeLayout()
}

function Do-Add {
    $r = Edit-Event $null
    if ($r) { $script:d.events += $r; Save-Data; Refresh-List }
}
function Do-Edit {
    $t = $script:target; if (-not $t) { return }
    $r = Edit-Event $t
    if ($r) { $t.name = $r.name; $t.date = $r.date; $t.image = $r.image; Save-Data; Refresh-List }
}
function Do-Delete {
    $t = $script:target; if (-not $t) { return }
    if ([Windows.Forms.MessageBox]::Show("'$($t.name)' 삭제할까요?", "HD", 'YesNo') -eq 'Yes') {
        $script:d.events = @($script:d.events | Where-Object { -not [object]::ReferenceEquals($_, $t) })
        Save-Data; Refresh-List
    }
}

$mAdd  = $menu.Items.Add("디데이 추가");  $mAdd.Add_Click({ Do-Add })
$mEdit = $menu.Items.Add("수정");        $mEdit.Add_Click({ Do-Edit })
$mDel  = $menu.Items.Add("삭제");        $mDel.Add_Click({ Do-Delete })
$null  = $menu.Items.Add("-")
$mTop  = $menu.Items.Add("항상 위에 표시"); $mTop.CheckOnClick = $true
$mTop.Add_Click({ $script:d.top = $mTop.Checked; $form.TopMost = $mTop.Checked; Save-Data })
$null  = $menu.Items.Add("-")
$mExit = $menu.Items.Add("HD 종료");     $mExit.Add_Click({ $form.Close() })
$menu.Add_Opening({
    $src = $menu.SourceControl
    $script:target = $(if ($src) { $src.Tag } else { $null })
    $has = [bool]$script:target
    $mEdit.Enabled = $has; $mDel.Enabled = $has
    $mTop.Checked = $script:d.top
})

$form.Add_FormClosing({
    if ($form.WindowState -eq 'Normal') {
        $script:d.x = $form.Location.X; $script:d.y = $form.Location.Y
        $script:d.w = $form.Size.Width;  $script:d.h = $form.Size.Height
    }
    Save-Data
})

$timer = New-Object Windows.Forms.Timer; $timer.Interval = 60000
$timer.Add_Tick({ Refresh-List }); $timer.Start()

Refresh-List
[Windows.Forms.Application]::Run($form)
