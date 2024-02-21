:: Variables sent from compiler
set TargetDir=%1
set SolutionDir=%2
set ProjectName=%3

:: Make sure the package directory exists
mkdir "%SolutionDir%package"

:: Copy all files needed for the package to the package directory
copy /Y "%TargetDir%%ProjectName%.dll" "%SolutionDir%package\%ProjectName%.dll"
copy /Y "%SolutionDir%manifest.json" "%SolutionDir%package\manifest.json"
copy /Y "%SolutionDir%README.md" "%SolutionDir%package\README.md"
copy /Y "%SolutionDir%CHANGELOG.md" "%SolutionDir%package\CHANGELOG.md"
copy /Y "%SolutionDir%icon.png" "%SolutionDir%package\icon.png"
