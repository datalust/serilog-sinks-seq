Push-Location $PSScriptRoot

if(Test-Path .\artifacts) { Remove-Item .\artifacts -Force -Recurse }

& dotnet restore --no-cache

$branch = @{ $true = $env:APPVEYOR_REPO_BRANCH; $false = $(git symbolic-ref --short -q HEAD) }[$env:APPVEYOR_REPO_BRANCH -ne $NULL];
$revision = @{ $true = "{0:00000}" -f [convert]::ToInt32("0" + $env:APPVEYOR_BUILD_NUMBER, 10); $false = "local" }[$env:APPVEYOR_BUILD_NUMBER -ne $NULL];
$suffix = @{ $true = ""; $false = "$branch-$revision"}[$branch -eq "master" -and $revision -ne "local"]

foreach ($src in ls src/Serilog.*) {
    Push-Location $src

	if ($suffix) {
		& dotnet pack -c Release -o ..\..\.\artifacts --version-suffix=$suffix --include-source
	} else {
	    & dotnet pack -c Release -o ..\..\.\artifacts --include-source
	}
    if($LASTEXITCODE -ne 0) { exit 1 }    

    Pop-Location
}

foreach ($test in ls test/Serilog.*.Tests) {
    Push-Location $test

    & dotnet test -c Release
    if($LASTEXITCODE -ne 0) { exit 2 }

    Pop-Location
}

Pop-Location
