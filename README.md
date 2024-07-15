# BTModMerger

A tool to transform [Barotrauma](https://barotraumagame.com) mods into a diff-like format and reapply them to "base" files, including multiple in a sequence. This allows to automatically make compatibility patches for mods that override the same prefab. In theory, this can also be useful for versioning the mod source to automatically reapply it to newer versions of the same base file.

## Prerequisites

To build: [.Net SDK 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

To run: [.Net SDK 8 or .NET Runtime 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

To run self-contained verision: nothing, I guess.

Only tested on windows 11, but should build just fine or with minor modifications for linux as well.

## How to use:

```
> BTModMerger -?
> BTModMerger -help
```

Show usage description like this.

```
> BTModMerger Diff base mod [output] [-alwaysOverride]
```

Transform a mod to moddiff format.

* base           Path to the base file mod will be diffed from.
* mod            Path to the mod file.
* output         Path to a file to store result into. If not provided, defaults to base.moddiff. Optional.
* alwaysOverride Interpret all elements as if they are inside of an override block. Somewhat useful for comparing mods. Optional.

```
> BTModMerger Apply [base] mod [output] [-asOverride]
```

Apply a mod in moddiff format to a base file. Can be done repeatedly.

* base      Path to the base file mod will be applied to. Optional. If not provided, cin would be used.
* mod       Path to the mod file.
* output    Path to a file to store result into. Optional. If not provided, cout would be used.
* override  Whether to generate a file with just overrides and additions (aka a mod) rather than all info. Optional, default: false.

```
> BTModMerger Indent [input] [output]
```

Indent an XML file.

* input    Path to a file to indent. If not provided, cin would be used.
* output   Path to a file to store result into. If not provided, cout would be used.

## Universal options, aka what can be added to any command

* PathToMetadata Path to a metadata file. Defaults to: exe_dir/BTMetadata.xml. If unset would be generated if missing.

## Example of usage

To parse an entire mod one can just use something like the following script:

```pwsh
$modId = 12345678
$merger = "C:\bin\BTModMerger\BTModMerger.exe"
$difsLocation = "c:\difs"
$regenLocation = "c:\regen"
$steamapps = "C:\Program Files (x86)\Steam\steamapps"

$baroContent = "$steamapps\common\Barotrauma\Content"
$modRoot = "$steamapps\workshop\content\602960\$modId\Override"
$modFiles = Get-ChildItem -Include *.xml -Recurse $modRoot

foreach ($file in $modFiles)
{
    $relative = Resolve-Path -Path $file -RelativeBasePath $modRoot -Relative
    $original = @()
    $original += Get-ChildItem -Recurse -Filter (Split-Path $relative -Leaf) $baroContent

    if ($original.Length -eq 1)
    {
        Write-Host "$merger Diff `"$original`" `"$file`" `"$difsLocation\$relative`""
        & $merger Diff $original $file $difsLocation\$relative
        Write-Host "$merger Apply `"$original`" `"$difsLocation\$relative`" `"$regenLocation\$relative`" -override"
        & $merger Apply $original $difsLocation\$relative $regenLocation\$relative -override
    }
    elseif ($original.Length -gt 1)
    {
        Write-Warning "More than a single original item found for $relative, ignoring"
    }
    else
    {
        Write-Warning "No original item found for $relative, ignoring"
    }
}
```

This would take every xml file in the mod, diff it with an appropriate base game file, store the result in c:\diffs ($difsLocation value)
Than it would take each diff and reapply it to the same base file, storing results to c:\regen ($regenLocation value)
This would effectively simplify each XML in mod, removing overrides that are 100% copy from vanilla

## Caveats

This is mostly irrelevant, but sometimes it is impossible to automatically determine whether some xml node group is an indexed array, meaning it can have multiple entities with either no identifiers or duplicate ones. For such cases it is possible to add them to `BTMetadata.xml` file that is next to the executable. If it is missing any successful invocation of `Diff` or `Apply` would regenerate it as an empty file. Simple cases of indexed collections (ones you do not expect to have items removed by mods from) can have their child items added to `Indexed` collection there. The rest should have them added to `Tricky`. Otherwise, indexing of items may be incorrect when applying diffs after other diffs with items removed.
