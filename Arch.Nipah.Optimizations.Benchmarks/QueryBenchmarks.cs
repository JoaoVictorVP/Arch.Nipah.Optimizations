using Arch.Core;
using Arch.System;
using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Arch.Nipah.Optimizations.Benchmarks;

[DisassemblyDiagnoser]
public class QueryBenchmarks
{
    readonly World world;
    readonly ArchExtendedSystem system;

    public QueryBenchmarks()
    {
        world = World.Create();

        var rand = new Random(100000);
        for(int i = 0; i < 1_000_000; i++)
        {
            world.Create(new Position(i, i * 3), new Size(i * 5, i * 9), new Life(rand.NextSingle() * 100));
        }

        system = new ArchExtendedSystem(world);
    }

    [Benchmark]
    public void DealDamageQuery()
    {
        var random = new Random(301012010);

        var query = new QueryDescription()
            .WithAll<Position, Size, Life>();

        world.Query(query, (Entity entity, ref Position pos, ref Size size, ref Life life) =>
        {
            pos = new Position(pos.X + 1, pos.Y + 1);
            size = new Size(size.Width + 1, size.Height + 1);
            life = new Life(life.Value - 1 + random.NextSingle());
        });
    }

    [Benchmark]
    [Optimize]
    public void DealDamageQueryOptimized()
    {
        var query = new QueryDescription()
            .WithAll<Position, Size, Life>();

        world.Query(query, static (Entity entity, ref Position pos, ref Size size, ref Life life) =>
        {
            var random = Optimizer.OutOfScope(() => new Random(301012010));

            pos = new Position(pos.X + 1, pos.Y + 1);
            size = new Size(size.Width + 1, size.Height + 1);
            life = new Life(life.Value - 1 + random.NextSingle());
        });
    }

    [Benchmark]
    public void DealDamageManuallyOptimized()
    {
        var random = new Random(301012010);

        var query = new QueryDescription()
            .WithAll<Position, Size, Life>();

        foreach(var chunk in world.Query(query).GetChunkIterator())
        {
            var arr = chunk.GetFirst<Position, Size, Life>();
            foreach(var index in chunk)
            {
                ref var entity = ref chunk.Entity(index);
                ref var pos = ref Unsafe.Add(ref arr.t0, index);
                ref var size = ref Unsafe.Add(ref arr.t1, index);
                ref var life = ref Unsafe.Add(ref arr.t2, index);

                pos = new Position(pos.X + 1, pos.Y + 1);
                size = new Size(size.Width + 1, size.Height + 1);
                life = new Life(life.Value - 1 + random.NextSingle());
            }
        }
    }

    [Benchmark]
    public void DealDamageManuallyOptimizedSimpler()
    {
        var random = new Random(301012010);

        var query = new QueryDescription()
            .WithAll<Position, Size, Life>();

        foreach (var chunk in world.Query(query).GetChunkIterator())
        {
            foreach (var index in chunk)
            {
                ref var entity = ref chunk.Entity(index);
                ref var pos = ref chunk.Get<Position>(index);
                ref var size = ref chunk.Get<Size>(index);
                ref var life = ref chunk.Get<Life>(index);

                pos = new Position(pos.X + 1, pos.Y + 1);
                size = new Size(size.Width + 1, size.Height + 1);
                life = new Life(life.Value - 1 + random.NextSingle());
            }
        }
    }

    [Benchmark]
    public void DealDamageQueryFromArchExtended()
    {
        system.ArchExtendedQueryQuery(world);
    }
}
public record struct Position(int X, int Y);
public record struct Size(int Width, int Height);
public record struct Life(float Value);

public partial class ArchExtendedSystem(World world) : BaseSystem<World, float>(world)
{
    private readonly Random random = new Random(301012010);

    [Query]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ArchExtendedQuery(in Entity entity, ref Position pos, ref Size size, ref Life life)
    {
        pos = new Position(pos.X + 1, pos.Y + 1);
        size = new Size(size.Width + 1, size.Height + 1);
        life = new Life(life.Value - 1 + random.NextSingle());
    }
}
