using namespace System.IO
using namespace System.Text

param([string]$exe_path, [string]$target_path = 'bin')
$ErrorActionPreference = 'Stop'
#$DebugPreference = 'Continue'

$exe_path = (Resolve-Path -Path $exe_path).Path

Write-Host "Origin path: `"$exe_path`""
Write-Host "Target dll path: $target_path"

$separator = '\'
$max_path_length = 1024

$exe_name = [Path]::GetFileName($exe_path)
$dll_name = [Path]::ChangeExtension($exe_name, '.dll')
Write-Debug "exe: $exe_name"
Write-Debug "dll: $dll_name"

function Update-Exe {
    $old_bytes = [Encoding]::UTF8.GetBytes("$dll_name`0")
    if ($old_bytes.Count -gt $max_path_length) {
        throw [PathTooLongException] 'old dll path is too long'
    }

    $new_dll_path = "$target_path$separator$dll_name"
    $new_bytes = [Encoding]::UTF8.GetBytes("$new_dll_path`0")
    Write-Host "Dll path Change to `"$new_dll_path`""
    if ($new_bytes.Count -gt $max_path_length) {
        throw [PathTooLongException] 'new dll path is too long'
    }

    $bytes = [File]::ReadAllBytes($exe_path)
    $index = (Get-Content $exe_path -Raw -Encoding 28591).IndexOf("$dll_name`0")
    if ($index -lt 0) {
        throw [InvalidDataException] 'Could not find old dll path'
    }
    Write-Debug "Position: $index"
    $end_postion = $index + $($new_bytes.Count)
    $end_length = $bytes.Count - $end_postion
    if ($end_postion -gt $bytes.Count) {
        throw [PathTooLongException] 'new dll path is too long'
    }
    Write-Debug "End Position: $end_postion"
    Write-Debug "End Length: $end_length"

    $fs = [File]::OpenWrite($exe_path)
    try {
        $fs.Write($bytes, 0, $index)
        $fs.Write($new_bytes)
        $fs.Write($bytes, $end_postion, $end_length)
    }
    finally {
        $fs.Dispose();
    }
}

function Move-Dll {
    $tmpbin = 'tmpbin'
    $dir = [Path]::GetDirectoryName($exe_path);
    $root = [Path]::GetDirectoryName($dir);
    Write-Debug "root path: $root"
    Write-Debug "dir path: $dir"

    Rename-Item $dir $tmpbin
    New-Item -ItemType Directory $dir > $null
    Move-Item $root\$tmpbin $dir
    Rename-Item $dir\$tmpbin $target_path
    Move-Item $dir\$target_path\$exe_name $dir
}

Update-Exe
Move-Dll