# BTModMerger

# How to use:

Diff <base> <mod> [<output>]  - Transform a mod to moddiff format

base     Path to the base file mod will be diffed from.
mod      Path to the mod file.
output   Path to a file to store result into. If not provided, defaults to <base>.moddiff. Optional.

Apply [<base>] <mod> [<output>]  - Merge a mod in moddiff format into a base file. Can be done repeatedly.

base     Path to the base file mod will be applied to. Optional. If not provided, cin would be used.
mod      Path to the mod file.
output   Path to a file to store result into. Optional. If not provided, cout would be used.

Indent [<input>] [<output>]  - Indent an XML file.

input    Path to a file to indent. If not provided, cin would be used.
output   Path to a file to store result into. If not provided, cout would be used.

