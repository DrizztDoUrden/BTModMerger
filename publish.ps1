Param(
    [Parameter(Mandatory = $true)]
    [String]$Version,
    [String]$MainArchiver = "C:\Program Files\7-Zip\7z.exe"
)

Remove-Item artifacts -Recurse -ErrorAction Ignore | Out-Null
New-Item -ItemType Directory -Force artifacts | Out-Null
$artifacts = (Get-Item artifacts).FullName

dotnet publish BTModMerger -p:PublishProfile=BTModMerger\Properties\PublishProfiles\Portable.pubxml && `
dotnet publish BTModMerger -p:PublishProfile=BTModMerger\Properties\PublishProfiles\SelfContained.pubxml && `
dotnet publish BTModMerger -p:PublishProfile=BTModMerger\Properties\PublishProfiles\SelfContainedLinux.pubxml && `
Push-Location BTModMerger\bin\publish && `
& $MainArchiver a -tzip $artifacts\BTModManager.$($Version).zip BTModManager && `
& $MainArchiver a -tzip $artifacts\BTModManager.self-contained.$($Version).zip BTModManager.self-contained && `
tar -czvf $artifacts\BTModManager.self-contained.linux.$($Version).tar.gz BTModManager.self-contained.linux && `
Pop-Location
