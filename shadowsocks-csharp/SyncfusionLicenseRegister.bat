@echo on&setlocal
    setlocal enableextensions disabledelayedexpansion

	::Arguments of either PreBuild or PostBuild
	set buildType=%1
	::License key replacement file
	set sourceFile=%2
	
	::Replacement string
    set DummyKey=##SyncfusionLicense##
	set LicenseKey=%SyncfusionLicenseKey%
	
	::Replacement statement
	if NOT "%LicenseKey%" == "" (
		if "%buildType%" == "PostBuild" (
		powershell -Command "(gc %sourceFile% -encoding UTF8) -Replace '%LicenseKey%','%DummyKey%'|SC %sourceFile% -encoding UTF8"
		)
		if "%buildType%" == "PreBuild" (
		powershell -Command "(gc %sourceFile% -encoding UTF8) -Replace '%DummyKey%','%LicenseKey%'|SC %sourceFile% -encoding UTF8"
		)
	)