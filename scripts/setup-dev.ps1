<#
  Kiểm tra và cài công cụ phát triển ToolQL (xem docs/dev-setup.md).
  - Chỉ cài thứ còn thiếu. Không sửa repo, không đụng tài khoản, không đọc file acc thử.
  - Ưu tiên winget; nếu máy không có winget (Windows 10 LTSC, Server) thì tải thẳng từ nguồn chính thức.
  - Cài vào thư mục người dùng (không cần Administrator) trừ khi dùng winget.
  - Windows PowerShell 5.1 trở lên.

  Dùng:  powershell -ExecutionPolicy Bypass -File scripts\setup-dev.ps1 [-CheckOnly]
#>
param(
    [switch]$CheckOnly,
    [string]$ToolsDir = 'C:\Dev\Tools',
    [string]$InputDir = 'C:\Dev\ToolQL-input'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest nhanh hơn nhiều khi tắt thanh tiến trình

$script:results = New-Object System.Collections.ArrayList
function Add-Result($tool, $status, $note = '') {
    [void]$script:results.Add([pscustomobject]@{ Tool = $tool; Status = $status; Note = $note })
}
function Test-Cmd($name) { return [bool](Get-Command $name -ErrorAction SilentlyContinue) }

# Nạp lại PATH của máy và người dùng vào phiên hiện tại để thấy ngay lệnh vừa cài.
function Update-SessionPath {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user"
    $jh = [Environment]::GetEnvironmentVariable('JAVA_HOME', 'User')
    if ($jh) { $env:JAVA_HOME = $jh }
}

function Add-UserPath($dir) {
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not $user) { $user = '' }
    if (($user -split ';') -notcontains $dir) {
        [Environment]::SetEnvironmentVariable('Path', ($dir + ';' + $user).TrimEnd(';'), 'User')
    }
    Update-SessionPath
}

function Get-Download($url, $outFile) {
    Invoke-WebRequest -Uri $url -OutFile $outFile -UseBasicParsing -Headers @{ 'User-Agent' = 'ToolQL-setup' }
}

function Get-GitHubAssets($repo) {
    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{ 'User-Agent' = 'ToolQL-setup' }
    return $rel
}

$hasWinget = Test-Cmd 'winget'
Add-Result 'winget' ($(if ($hasWinget) { 'CÓ' } else { 'KHÔNG CÓ' })) ($(if ($hasWinget) { '' } else { 'bình thường trên Windows LTSC/Server; script dùng tải trực tiếp' }))

# ---------- Git ----------
function Install-Git {
    if ($hasWinget) {
        winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements | Out-Host
    } else {
        $rel = Get-GitHubAssets 'git-for-windows/git'
        $asset = $rel.assets | Where-Object { $_.name -match '^Git-.*-64-bit\.exe$' } | Select-Object -First 1
        if (-not $asset) { throw 'không tìm thấy bộ cài Git 64-bit trong bản phát hành mới nhất' }
        $tmp = Join-Path $env:TEMP $asset.name
        Get-Download $asset.browser_download_url $tmp
        Start-Process -FilePath $tmp -ArgumentList '/VERYSILENT', '/NORESTART', '/SUPPRESSMSGBOXES' -Wait
    }
    Update-SessionPath
}

# ---------- .NET 8 SDK ----------
function Install-DotNet {
    if ($hasWinget) {
        winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements | Out-Host
    } else {
        $script = Join-Path $env:TEMP 'dotnet-install.ps1'
        Get-Download 'https://dot.net/v1/dotnet-install.ps1' $script
        $dir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
        & $script -Channel 8.0 -InstallDir $dir | Out-Host
        [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $dir, 'User')
        $env:DOTNET_ROOT = $dir
        Add-UserPath $dir
    }
    Update-SessionPath
}

# ---------- JDK 25 (Temurin) cho Recaf (cần Java 22 trở lên) ----------
function Install-Jdk {
    if ($hasWinget) {
        winget install --id EclipseAdoptium.Temurin.25.JDK -e --accept-source-agreements --accept-package-agreements | Out-Host
    } else {
        $zip = Join-Path $env:TEMP 'temurin-25-jdk.zip'
        Get-Download 'https://api.adoptium.net/v3/binary/latest/25/ga/windows/x64/jdk/hotspot/normal/eclipse' $zip
        $dest = Join-Path $ToolsDir 'jdk-25'
        $tmpDir = Join-Path $env:TEMP 'jdk25-extract'
        if (Test-Path $tmpDir) { Remove-Item $tmpDir -Recurse -Force }
        Expand-Archive -Path $zip -DestinationPath $tmpDir -Force
        $inner = Get-ChildItem $tmpDir -Directory | Select-Object -First 1
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
        Move-Item $inner.FullName $dest
        [Environment]::SetEnvironmentVariable('JAVA_HOME', $dest, 'User')
        $env:JAVA_HOME = $dest
        Add-UserPath (Join-Path $dest 'bin')
    }
    Update-SessionPath
}

# ---------- Recaf Launcher ----------
function Install-Recaf {
    $dest = Join-Path $ToolsDir 'Recaf'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $rel = Get-GitHubAssets 'Col-E/Recaf-Launcher'
    $jars = @($rel.assets | Where-Object { $_.name -match '\.jar$' })
    if ($jars.Count -eq 0) { throw 'không thấy file .jar trong bản phát hành của Recaf-Launcher; tải tay theo docs/dev-setup.md' }
    foreach ($a in $jars) { Get-Download $a.browser_download_url (Join-Path $dest $a.name) }
}

function Ensure($tool, $isPresent, $install, $note = '') {
    if (& $isPresent) { Add-Result $tool 'OK' $note; return }
    if ($CheckOnly) { Add-Result $tool 'THIẾU' $note; return }
    try {
        Write-Host "Đang cài $tool ..."
        & $install
        if (& $isPresent) { Add-Result $tool 'ĐÃ CÀI' $note }
        else { Add-Result $tool 'ĐÃ CÀI?' 'mở terminal mới rồi chạy lại script để xác nhận' }
    } catch {
        Add-Result $tool 'LỖI' ($_.Exception.Message + ' -> cài tay theo docs/dev-setup.md')
    }
}

Update-SessionPath

Ensure 'Git' { Test-Cmd 'git' } { Install-Git }

Ensure '.NET SDK 8.x' {
    (Test-Cmd 'dotnet') -and ((& dotnet --list-sdks 2>$null) -match '^8\.')
} { Install-DotNet }

# Java: Recaf cần 22+. Chấp nhận bản >= 22.
function Get-JavaMajor {
    if (-not (Test-Cmd 'java')) { return 0 }
    $line = (& cmd /c 'java -version 2>&1') | Select-Object -First 1
    if ($line -match '"(\d+)(\.\d+)*') { return [int]$Matches[1] }
    return 0
}
Ensure 'JDK 22+ (cho Recaf)' { (Get-JavaMajor) -ge 22 } { Install-Jdk } ("hiện tại: Java " + (Get-JavaMajor))

Ensure 'Recaf Launcher' { Test-Path (Join-Path $ToolsDir 'Recaf\*.jar') } { Install-Recaf } (Join-Path $ToolsDir 'Recaf')

# ---------- Chỉ kiểm tra, không tự cài ----------
if (Test-Cmd 'claude') { Add-Result 'Claude Code' 'OK' ((& claude --version 2>$null) -join ' ') }
else { Add-Result 'Claude Code' 'THIẾU' 'chủ repo cài: winget install Anthropic.ClaudeCode hoặc irm https://claude.ai/install.ps1 | iex' }

$vcKey = 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
if ((Test-Path $vcKey) -and ((Get-ItemProperty $vcKey).Installed -eq 1)) { Add-Result 'VC++ 2015-2022 x64 (máy này)' 'OK' 'VPS 2012 R2 cũng phải có, xem docs/dev-setup.md mục 6' }
else { Add-Result 'VC++ 2015-2022 x64 (máy này)' 'THIẾU' 'cài vc_redist.x64.exe từ aka.ms/vs/17/release/vc_redist.x64.exe' }

if (Test-Path $InputDir) { Add-Result 'Thư mục client (ngoài repo)' 'OK' $InputDir }
else { Add-Result 'Thư mục client (ngoài repo)' 'THIẾU' "chủ repo tạo $InputDir và chép jar vào" }

# File acc thử: chỉ kiểm tra tồn tại, KHÔNG đọc hay in nội dung.
$envFile = Join-Path $env:USERPROFILE '.config\toolql\test-account.env'
if (Test-Path $envFile) { Add-Result 'File acc thử' 'OK' 'có (không in nội dung)' }
else { Add-Result 'File acc thử' 'CHƯA CÓ' 'tùy chọn; chủ repo tạo khi cho phép dùng acc thử' }

Write-Host ''
$results | Format-Table -AutoSize -Wrap
if ($results | Where-Object { $_.Status -in 'THIẾU', 'LỖI', 'ĐÃ CÀI?' }) {
    Write-Host 'Còn mục chưa đạt. Mở terminal mới rồi chạy lại script; nếu vẫn thiếu, làm tay theo docs/dev-setup.md.'
    exit 1
}
Write-Host 'Môi trường đủ.'
