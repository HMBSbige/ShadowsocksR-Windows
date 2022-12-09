param([string]$buildtfm = 'all')
$ErrorActionPreference = 'Stop'

Write-Host 'dotnet SDK info'
dotnet --info

$exe = 'ShadowsocksR.exe'
$net_tfm = 'net7.0-windows'
$configuration = 'Release'
$output_dir = "$PSScriptRoot\shadowsocks-csharp\bin\$configuration"
$proj_path = "$PSScriptRoot\shadowsocks-csharp\shadowsocksr.csproj"

$build    = $buildtfm -eq 'all' -or $buildtfm -eq 'app'
$buildX86 = $buildtfm -eq 'all' -or $buildtfm -eq 'x86'
$buildX64 = $buildtfm -eq 'all' -or $buildtfm -eq 'x64'
function Build-App
{
	Write-Host 'Building .NET App'
	
	$outdir = "$output_dir\$net_tfm"
	$publishDir = "$outdir\publish"

	Remove-Item $publishDir -Recurse -Force -Confirm:$false -ErrorAction Ignore
	
	dotnet publish -c $configuration -f $net_tfm $proj_path
	if ($LASTEXITCODE) { exit $LASTEXITCODE }

	& "$PSScriptRoot\Build\DotNetDllPathPatcher.ps1" $publishDir\$exe bin
	if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

function Build-SelfContained
{
	param([string]$rid)

	Write-Host "Building .NET App SelfContained $rid"

	$outdir = "$output_dir\$net_tfm\$rid"
	$publishDir = "$outdir\publish"

	Remove-Item $publishDir -Recurse -Force -Confirm:$false -ErrorAction Ignore

	dotnet publish -c $configuration -f $net_tfm -r $rid --self-contained true $proj_path
	if ($LASTEXITCODE) { exit $LASTEXITCODE }

	& "$PSScriptRoot\Build\DotNetDllPathPatcher.ps1" $publishDir\$exe bin
	if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

if ($build)
{
	Build-App
}

if ($buildX64)
{
	Build-SelfContained win-x64
}

if ($buildX86)
{
	Build-SelfContained win-x86
}