# BTModMerger

A tool to transform [Barotrauma](https://barotraumagame.com) mods into a diff-like format and reapply them to "base" files. This allows to automatically make compatibility patches for mods that override the same prefab. In theory, this can also be useful for versioning the mod source to automatically reapply it to newer versions of the same base file.

## Prerequisites

To build: [.Net SDK 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
To run: either [.Net SDK 8 or .NET Runtime 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
To run self-contained verision: nothing, I guess.
Only tested on windows 11, but should build just fine or with minor modifications for linux as well.

## How to use:

```
> BTModMerger -?
> BTModMerger -help
```

Show usage description like this.

```
> BTModMerger Diff base mod [output]
```

Transform a mod to moddiff format.

* base     Path to the base file mod will be diffed from.
* mod      Path to the mod file.
* output   Path to a file to store result into. If not provided, defaults to base.moddiff. Optional.

```
> BTModMerger Apply [base] mod [output]
```

Merge a mod in moddiff format into a base file. Can be done repeatedly.

* base     Path to the base file mod will be applied to. Optional. If not provided, cin would be used.
* mod      Path to the mod file.
* output   Path to a file to store result into. Optional. If not provided, cout would be used.

```
> BTModMerger Indent [input] [output]
```

Indent an XML file.

* input    Path to a file to indent. If not provided, cin would be used.
* output   Path to a file to store result into. If not provided, cout would be used.

## Caveats

It is imposible to automatically determine wether some xml node group is an indexed array, meaning it can have multiple entities with either no identifers or duplicate ones. For such cases it is possible to add them to `BTMetadata.xml` file that is next to the executable. If it is missing any successful invokation of `Diff` or `Apply` would regenerate it. Simple cases of indexed collections (ones you do not expect to have items removed by mods from) can have their child items added to `Indexed` collection there. The rest should have them added to `Tricky`. Otherwise, indexing of items may be incorrect when applying diffs after other diffs with items removed.
