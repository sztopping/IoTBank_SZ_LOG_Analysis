# 发布脚本：生成自包含单文件 exe，拷贝到任意 Win10+ 电脑直接运行（无需安装 .NET 运行时）
# 用法：在项目根目录执行  .\publish.ps1
$ErrorActionPreference = "Stop"

# 从 ChangeLog.cs 读取当前版本号
$version = "V0.0.0"
$changeLog = Get-Content -Raw -Path "LogAnalyzer\ChangeLog.cs" -Encoding UTF8
if ($changeLog -match 'CurrentVersion = "([^"]+)"') {
    $version = $Matches[1]
}
Write-Host "当前版本：$version"

# 清理旧发布目录
if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force }

# 发布：自包含 + 单文件（win-x64，Release）
Write-Host "正在发布自包含单文件 (win-x64)..."
dotnet publish "LogAnalyzer\LogAnalyzer.csproj" -c Release -r win-x64 --self-contained true -o publish
if ($LASTEXITCODE -ne 0) { Write-Error "发布失败"; exit 1 }

# 重命名 exe 带版本号
$exe = "publish\LogAnalyzer.exe"
if (Test-Path $exe) {
    $newName = "publish\LogAnalyzer_$version.exe"
    Move-Item $exe $newName -Force
    Write-Host "已生成：$newName"
}

# 删除调试符号（可选）
Get-ChildItem "publish" -Filter "*.pdb" -Recurse | Remove-Item -Force

Write-Host ""
Write-Host "发布完成。拷贝 publish 目录（或单个 exe + docs 文件夹）到任意 Win10+ 电脑即可运行。"
Write-Host "说明：docs 文件夹含操作说明(SOP)与全代码文档，可随 exe 一起分发。"
