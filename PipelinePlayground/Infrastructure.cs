using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PipelinePlayground;

public interface IBehavior {}

public interface IBehaviorContext
{
    PipelineFrame Frame { get; }
}

class BehaviorContext : IBehaviorContext
{
    internal required IBehavior[] Behaviors { get; set; } = [];

    public PipelineFrame Frame { get; } = new PipelineFrame
    {
        Parts = [],
        Index = 0,
        Parent = null,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TBehavior GetBehavior<TBehavior>(int index)
        where TBehavior : class, IBehavior
        => Unsafe.As<TBehavior>(
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Behaviors), index));
}


public sealed class PipelineFrame
{
    public PipelinePart[] Parts = [];
    public int Index;

    // Stack for connectors
    public PipelineFrameSnapshot? Parent;
}

public sealed class PipelineFrameSnapshot
{
    public required PipelinePart[] Parts;
    public required int Index;
    public PipelineFrameSnapshot? Parent;
}

public abstract class PipelinePart
{
    public abstract Task Invoke(IBehaviorContext context);
}

public static class StageRunners
{
    public static Task Start(IBehaviorContext ctx, PipelinePart[] parts)
    {
        var f = ctx.Frame;
        f.Parts = parts;
        f.Index = 0;

        return parts.Length == 0 ? Complete(ctx) : parts[0].Invoke(ctx);
    }

    public static Task Next(IBehaviorContext ctx)
    {
        var f = ctx.Frame;
        var nextIndex = ++f.Index;

        return (uint)nextIndex < (uint)f.Parts.Length ? f.Parts[nextIndex].Invoke(ctx) : Complete(ctx);
    }

    static Task Complete(IBehaviorContext ctx)
    {
        var f = ctx.Frame;
        var parent = f.Parent;

        if (parent is null)
        {
            return Task.CompletedTask;
        }

        f.Parts = parent.Parts;
        f.Index = parent.Index;
        f.Parent = parent.Parent;

        return Next(ctx);
    }
}

public abstract class BehaviorPart<TContext, TBehavior>(int behaviorIndex) : PipelinePart
    where TContext : class, IBehaviorContext
    where TBehavior : class, IBehavior<TContext, TContext>
{
    public override Task Invoke(IBehaviorContext context)
    {
        var ctx = (TContext)context;
        var behavior = (ctx as BehaviorContext)!.GetBehavior<TBehavior>(behaviorIndex);
        return behavior.Invoke(ctx, static ctx => StageRunners.Next(ctx));
    }
}

public interface IBehavior<in TInContext, out TOutContext> : IBehavior
    where TInContext : IBehaviorContext
{
    Task Invoke(TInContext context, Func<TOutContext, Task> next);
}

public interface IStage1Context : IBehaviorContext;

class Stage1Context : BehaviorContext, IStage1Context;

public interface IStage2Context : IBehaviorContext;

class Stage2Context : BehaviorContext, IStage2Context;

public class Stage1Behavior : IBehavior<IStage1Context, IStage1Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage1Context, Task> next)
    {
        await next(context);
    }
}

class Stage1ToStage2Behavior : IBehavior<IStage1Context, IStage2Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage2Context, Task> next)
    {
        await next(new Stage2Context
        {
            Behaviors = ((BehaviorContext)context).Behaviors
        });
    }
}

public class Stage2Behavior : IBehavior<IStage2Context, IStage2Context>
{
    public async Task Invoke(IStage2Context context, Func<IStage2Context, Task> next)
    {
        await next(context);
    }
}