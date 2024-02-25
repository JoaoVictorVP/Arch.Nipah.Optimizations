using Arch.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arch.Nipah.Optimizations.Benchmarks;

public readonly struct TestComp;

public static class SimpleTests
{
    [Optimize]
    public static void System()
    {
        using var world = World.Create();

        var query = new QueryDescription()
            .WithAll<TestComp>();

        world.Query(query, (ref TestComp comp) =>
        {
            Console.WriteLine("Has comp");
        });
    }
}
