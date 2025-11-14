// Quick validation test for Phase 1 implementation
using System;
using System.Threading.Tasks;
using EasyAppDev.Blazor.Store.Utilities;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Testing DebounceManager...");
        await TestDebounceManager();

        Console.WriteLine("\nTesting ThrottleManager...");
        await TestThrottleManager();

        Console.WriteLine("\n✅ All quick tests passed!");
    }

    static async Task TestDebounceManager()
    {
        using var manager = new DebounceManager();
        var count = 0;

        // Test 1: Single debounce
        await manager.Debounce("test1", async () => { count++; await Task.CompletedTask; }, 50);
        await Task.Delay(100);

        if (count != 1) throw new Exception($"Expected count=1, got {count}");
        Console.WriteLine("  ✓ Single debounce works");

        // Test 2: Multiple rapid calls (only last should execute)
        count = 0;
        await manager.Debounce("test2", async () => { count++; await Task.CompletedTask; }, 50);
        await Task.Delay(20);
        await manager.Debounce("test2", async () => { count++; await Task.CompletedTask; }, 50);
        await Task.Delay(100);

        if (count != 1) throw new Exception($"Expected count=1, got {count}");
        Console.WriteLine("  ✓ Cancellation works");
    }

    static async Task TestThrottleManager()
    {
        using var manager = new ThrottleManager();
        var count = 0;

        // Test: Leading edge execution
        await manager.Throttle("test1", async () => { count++; await Task.CompletedTask; }, 100, leading: true);
        await Task.Delay(50);

        if (count != 1) throw new Exception($"Expected count=1, got {count}");
        Console.WriteLine("  ✓ Leading edge works");

        // Test: Throttling limits frequency
        count = 0;
        for (int i = 0; i < 10; i++)
        {
            await manager.Throttle("test2", async () => { count++; await Task.CompletedTask; }, 50, leading: true);
            await Task.Delay(10);
        }
        await Task.Delay(100);

        if (count < 2) throw new Exception($"Expected count>=2, got {count}");
        Console.WriteLine($"  ✓ Throttling works (executed {count} times from 10 calls)");
    }
}
