# 发布脚本：生成 .NET Framework 4.8 发布目录，拷贝到任意 Win10+ 电脑直接运行
# （Win10/11 系统自带 .NET Framework 4.8，无需安装运行时，发布体积约 3~4MB）
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

# 发布（.NET Framework 4.8，无需 self-contained）
Write-Host "正在发布 .NET Framework 4.8 ..."
dotnet publish "LogAnalyzer\LogAnalyzer.csproj" -c Release -o publish
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
Write-Host "发布完成。拷贝 publish 目录（约 3~4MB）到任意 Win10+ 电脑即可运行，无需安装运行时。"
Write-Host "说明：docs 文件夹含操作说明(SOP)与全代码文档；ai_config.json 为可选 AI 配置。"
