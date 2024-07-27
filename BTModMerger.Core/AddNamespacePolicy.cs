using PowerArgs;

namespace BTModMerger.Core;

public enum AddNamespacePolicy
{
    [ArgDescription("Leave all add: namespaces as they are.")]
    Preserve,
    [ArgDescription("Add add: namespaces wherever applicable")]
    Add,
    [ArgDescription("Remove add: namespace everywhere")]
    Remove,
}
