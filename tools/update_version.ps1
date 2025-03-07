Param(
	[Parameter(Mandatory = $true)][string]
	$path,
	[string]
	$config = "Release",
	[string]
	$platform = "x64"
)

$exists = Test-Path $path
$version = 6,4,1,0

# Get version from existing file
# if ($exists -and $(Get-Content $path | Out-String) -match "VERSION_FULL (\d+).(\d+).(\d+).(\d+)") {
# 	$version = [int]::Parse($matches[1]), [int]::Parse($matches[2]), [int]::Parse($matches[3]), [int]::Parse($matches[4])
# }
# elseif ($(git describe --tags) -match "v(\d+)\.(\d+)\.(\d+)(-\d+-\w+)?") {
# 	$version = [int]::Parse($matches[1]), [int]::Parse($matches[2]), [int]::Parse($matches[3]), 0
# }

$build = $(git rev-parse --short HEAD)
$cn2 = "Release 9.0"
$cn2_pro = "Pro 9.0"

# Increment build version for release builds
if (($config -eq "Release") -or
    ($config -eq "Release Signed")) {
	# $version[3] += 1
	"Updating version to $([string]::Join('.', $version)) ..."
}
elseif ($exists) {
	return
}

$official = Test-Path ($path + "\..\sign.pfx")

# Update version file with the new version information
@"
#pragma once

#define VERSION_FULL $([string]::Join('.', $version))
#define VERSION_MAJOR $($version[0])
#define VERSION_MINOR $($version[1])
#define VERSION_REVISION $($version[2])
#define VERSION_BUILD $($version[3])

#define VERSION_STRING_FILE "$([string]::Join('.', $version))"
#define VERSION_STRING_PRODUCT "$($version[0]).$($version[1]).$($version[2])"

#ifdef AURORA_PRO
#define VERSION_STRING_CN2 "$cn2_pro | Build $build"
#else
#define VERSION_STRING_CN2 "$cn2 | Build $build"
#endif
"@ | Out-File -FilePath $path -Encoding ASCII
