param([string]$buildtfm = 'all')
$ErrorActionPreference = 'Stop'

Write-Host 'dotnet SDK version'
dotnet --version

$exe = 'ShadowsocksR.exe'
$netcore_tfm = 'netcoreapp3.1'
$configuration = 'Release'
$output_dir = "shadowsocks-csharp\bin\$configuration"
$dllpatcher_dir = "Build\DotNetDllPathPatcher"
$proj_path = 'shadowsocks-csharp\shadowsocksr.csproj'

$buildCore    = $buildtfm -eq 'all' -or $buildtfm -eq 'core'
$buildCoreX86 = $buildtfm -eq 'all' -or $buildtfm -eq 'core-x86'
$buildCoreX64 = $buildtfm -eq 'all' -or $buildtfm -eq 'core-x64'
function Build-NetCore
{
	Write-Host 'Building .NET Core'
	
	$outdir = "$output_dir\$netcore_tfm"
	$publishDir = "$outdir\publish"

	Remove-Item $publishDir -Recurse -Force -Confirm:$false -ErrorAction Ignore
	
	dotnet publish -c $configuration -f $netcore_tfm $proj_path
	if ($LASTEXITCODE) { exit $LASTEXITCODE }

	& $dllpatcher_dir\bin\$configuration\$netcore_tfm\DotNetDllPathPatcher.exe $publishDir\$exe bin
	if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

function Build-NetCoreSelfContained
{
	param([string]$arch)

	Write-Host "Building .NET Core $arch"

	$rid = "win-$arch"
	$outdir = "$output_dir\$netcore_tfm\$rid"
	$publishDir = "$outdir\publish"

	Remove-Item $publishDir -Recurse -Force -Confirm:$false -ErrorAction Ignore

	dotnet publish -c $configuration -f $netcore_tfm -r $rid --self-contained true $proj_path
	if ($LASTEXITCODE) { exit $LASTEXITCODE }

	& $dllpatcher_dir\bin\$configuration\$netcore_tfm\DotNetDllPathPatcher.exe $publishDir\$exe bin
	if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

dotnet build -c $configuration -f $netcore_tfm $dllpatcher_dir\DotNetDllPathPatcher.csproj
if ($LASTEXITCODE) { exit $LASTEXITCODE }

if ($buildCore)
{
	Build-NetCore
}

if ($buildCoreX86)
{
	Build-NetCoreSelfContained x86
}

if ($buildCoreX64)
{
	Build-NetCoreSelfContained x64
}