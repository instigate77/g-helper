n# ProcessModeTrayWatcher.ps1
# Tray-based process watcher that sets G-Helper mode via IPC
# - Tray menu: Reload Config, Open Config, Toggle Logging, Quit
# - Auto-reloads when config file changes
# - Debounce to avoid flapping

param(
  [string]$ConfigPath = "$PSScriptRoot\process-modes.json"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Read-Config {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path)) { throw "Config not found: $Path" }
  Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Ensure-GHelper {
  param([string]$ExePath, [bool]$Log)
  $running = Get-Process -Name "GHelper" -ErrorAction SilentlyContinue
  if (-not $running) {
    try {
      Start-Process -FilePath $ExePath -ArgumentList "-log" -WindowStyle Hidden
      Start-Sleep -Milliseconds 800
      if ($Log) { Write-Host "[$(Get-Date -Format HH:mm:ss)] Started GHelper" }
    } catch { if ($Log) { Write-Warning "Failed to start GHelper: $_" } }
  }
}

function Is-AnyProcessRunning {
  param([string[]]$Names)
  foreach ($n in $Names) { try { if (Get-Process -Name $n -ErrorAction SilentlyContinue) { return $true } } catch {} }
  return $false
}

function Pick-TargetMode {
  param($cfg)
  $best = $null
  foreach ($map in $cfg.mappings) {
    if (Is-AnyProcessRunning -Names $map.processes) {
      if (-not $best -or $map.priority -gt $best.priority) { $best = $map }
    }
  }
  if ($best) { return $best.mode }
  return $cfg.defaultMode
}

function Send-Mode {
  param([string]$ExePath, [string]$Mode, [bool]$Log)
  try {
    Start-Process -FilePath $ExePath -ArgumentList "-mode $Mode" -WindowStyle Hidden
    if ($Log) { Write-Host "[$(Get-Date -Format HH:mm:ss)] Mode -> $Mode" }
  } catch { if ($Log) { Write-Warning "Failed to send mode '$Mode': $_" } }
}

$cfg = Read-Config -Path $ConfigPath
if (-not $cfg.pollIntervalMs) { $cfg.pollIntervalMs = 3000 }
if (-not $cfg.stabilizeMs) { $cfg.stabilizeMs = 4000 }
if ($null -eq $cfg.log) { $cfg.log = $true }

$lastIntended = $null
$lastApplied = $null
$sinceChange = [datetime]::UtcNow
$logEnabled = [bool]$cfg.log

$icon = New-Object System.Windows.Forms.NotifyIcon
$icon.Icon = [System.Drawing.SystemIcons]::Application
$icon.Text = "Process Mode Watcher"
$icon.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip
$miReload = $menu.Items.Add("Reload Config")
$miOpen   = $menu.Items.Add("Open Config")
$miLog    = $menu.Items.Add(("Logging: " + ($(if($logEnabled){"On"}else{"Off"}))))
$menu.Items.Add("-")
$miQuit   = $menu.Items.Add("Quit")

$miReload.add_Click({
  try {
    $script:cfg = Read-Config -Path $ConfigPath
    if (-not $cfg.pollIntervalMs) { $cfg.pollIntervalMs = 3000 }
    if (-not $cfg.stabilizeMs) { $cfg.stabilizeMs = 4000 }
    if ($null -eq $cfg.log) { $cfg.log = $true }
    $script:logEnabled = [bool]$cfg.log
    $miLog.Text = "Logging: " + ($(if($script:logEnabled){"On"}else{"Off"}))
    if ($script:logEnabled) { [System.Windows.Forms.MessageBox]::Show("Config reloaded.","Process Mode Watcher") | Out-Null }
  } catch { [System.Windows.Forms.MessageBox]::Show("Failed to reload config: $($_.Exception.Message)","Process Mode Watcher") | Out-Null }
})

$miOpen.add_Click({ Start-Process powershell -ArgumentList "-NoProfile -Command `"ii '$ConfigPath'`"" })

$miLog.add_Click({
  $script:logEnabled = -not $script:logEnabled
  $miLog.Text = "Logging: " + ($(if($script:logEnabled){"On"}else{"Off"}))
})

$miQuit.add_Click({
  $icon.Visible = $false
  $timer.Stop()
  if ($fsw) { $fsw.EnableRaisingEvents = $false; $fsw.Dispose() }
  [System.Windows.Forms.Application]::Exit()
})

$icon.ContextMenuStrip = $menu
$icon.add_MouseUp({ param($sender, $e) if ($e.Button -eq [System.Windows.Forms.MouseButtons]::Left) { $pt = [System.Windows.Forms.Control]::MousePosition; $menu.Show($pt) } })

$fsw = New-Object System.IO.FileSystemWatcher
$fsw.Path = [System.IO.Path]::GetDirectoryName($ConfigPath)
$fsw.Filter = [System.IO.Path]::GetFileName($ConfigPath)
$fsw.NotifyFilter = [System.IO.NotifyFilters]'LastWrite, Size, FileName'
$fsw.IncludeSubdirectories = $false
$fsw.EnableRaisingEvents = $true
Register-ObjectEvent -InputObject $fsw -EventName Changed -SourceIdentifier "CfgChanged" -Action {
  try {
    Start-Sleep -Milliseconds 300
    $cfgNew = Get-Content -Raw -LiteralPath $ConfigPath | ConvertFrom-Json
    if (-not $cfgNew.pollIntervalMs) { $cfgNew.pollIntervalMs = 3000 }
    if (-not $cfgNew.stabilizeMs) { $cfgNew.stabilizeMs = 4000 }
    if ($null -eq $cfgNew.log) { $cfgNew.log = $true }
    $script:cfg = $cfgNew
    $script:logEnabled = [bool]$cfgNew.log
    $miLog.Text = "Logging: " + ($(if($script:logEnabled){"On"}else{"Off"}))
    if ($script:logEnabled) { Write-Host "[$(Get-Date -Format HH:mm:ss)] Config changed -> reloaded" }
  } catch {}
}

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = [Math]::Max(500, [int]$cfg.pollIntervalMs)
$timer.Add_Tick({
  try {
    Ensure-GHelper -ExePath $cfg.gHelperPath -Log $logEnabled

    $intended = Pick-TargetMode -cfg $cfg
    if ($intended -ne $lastIntended) {
      $lastIntended = $intended
      $sinceChange = [datetime]::UtcNow
      if ($logEnabled) { Write-Host "[$(Get-Date -Format HH:mm:ss)] Target -> $intended (stabilizing...)" }
    }

    $stableForMs = ([datetime]::UtcNow - $sinceChange).TotalMilliseconds
    if ($stableForMs -ge [double]$cfg.stabilizeMs -and $intended -ne $lastApplied) {
      Send-Mode -ExePath $cfg.gHelperPath -Mode $intended -Log $logEnabled
      $lastApplied = $intended
      $icon.Text = "Process Mode Watcher ($lastApplied)"
    }
  } catch { if ($logEnabled) { Write-Warning "Tick error: $_" } }
})
$timer.Start()

[System.Windows.Forms.Application]::EnableVisualStyles()
[System.Windows.Forms.Application]::Run()

Unregister-Event -SourceIdentifier "CfgChanged" -ErrorAction SilentlyContinue
$fsw.Dispose()
$icon.Dispose()
