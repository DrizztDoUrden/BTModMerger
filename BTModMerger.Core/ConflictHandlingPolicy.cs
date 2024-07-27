using PowerArgs;

namespace BTModMerger.Core;

public enum ConflictHandlingPolicy
{
    [ArgDescription("Override conflicts with the value from later file")]
    Override,
    [ArgDescription("Produce an error on conflict")]
    Error,
}
