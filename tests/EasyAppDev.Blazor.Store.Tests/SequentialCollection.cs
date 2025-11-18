using Xunit;

namespace EasyAppDev.Blazor.Store.Tests;

/// <summary>
/// Collection definition for timing-sensitive tests that must run sequentially.
/// Tests in this collection will not run in parallel with each other.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection
{
}
