using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PipelinePlayground;

public interface IBehavior;

public interface IBehaviorContext;

class BehaviorContext : IBehaviorContext
{
    protected BehaviorContext(IBehaviorContext? parent = null)
    {
        if (parent is BehaviorContext parentContext)
        {
            Behaviors = parentContext.Behaviors;
            Parts = parentContext.Parts;
            CurrentIndex = parentContext.CurrentIndex;
        }
        else
        {
            Behaviors = [];
            Parts = [];
            CurrentIndex = 0;
        }
    }

    internal IBehavior[] Behaviors { get; init; }
    internal PipelinePart[] Parts;
    internal int CurrentIndex;

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TBehavior GetBehavior<TBehavior>(int index)
        where TBehavior : class, IBehavior
        => Unsafe.As<TBehavior>(
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Behaviors), index));
}

public abstract class PipelinePart
{
    public abstract Task Invoke(IBehaviorContext context);
}

public static class StageRunners
{
    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Start(IBehaviorContext ctx, PipelinePart[] parts)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        context.Parts = parts;
        context.CurrentIndex = 0;

        return parts.Length == 0
            ? Task.CompletedTask
            : Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(parts), 0).Invoke(ctx);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Next<TContext>(TContext ctx)
        where TContext : class, IBehaviorContext
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        var parts = context.Parts;
        var nextIndex = ++context.CurrentIndex;

        return (uint)nextIndex >= (uint)parts.Length
            ? Task.CompletedTask
            : Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(parts), nextIndex).Invoke(ctx);
    }
}

public abstract class BehaviorPart<TContext, TBehavior> : PipelinePart
    where TContext : class, IBehaviorContext
    where TBehavior : class, IBehavior<TContext, TContext>
{
    private static readonly Func<TContext, Task> CachedNext = StageRunners.Next;

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override Task Invoke(IBehaviorContext context)
    {
        var ctx = Unsafe.As<BehaviorContext>(context);
        var behavior = ctx.GetBehavior<TBehavior>(ctx.CurrentIndex);
        return behavior.Invoke(Unsafe.As<TContext>(context), CachedNext);
    }
}

/// <summary>
/// Stage transition part. The nextStageStartIndex indicates where the next stage begins in the parts array.
/// </summary>
public abstract class StagePart<TInContext, TOutContext, TBehavior>(int nextStageStartIndex) : PipelinePart
    where TInContext : class, IBehaviorContext
    where TOutContext : class, IBehaviorContext
    where TBehavior : class, IBehavior<TInContext, TOutContext>
{
    private static readonly Func<TOutContext, Task> CachedNext = StartNextStage;

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    public sealed override Task Invoke(IBehaviorContext context)
    {
        var ctx = Unsafe.As<BehaviorContext>(context);
        var behavior = ctx.GetBehavior<TBehavior>(ctx.CurrentIndex);
        // Set the next stage start index so the cached delegate knows where to jump
        ctx.CurrentIndex = nextStageStartIndex - 1; // -1 because Next will increment

        return behavior.Invoke(Unsafe.As<TInContext>(context), CachedNext);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task StartNextStage(TOutContext ctx)
    {
        return StageRunners.Next(ctx);
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

class Stage2Context(IStage1Context context) : BehaviorContext(context), IStage2Context;

public sealed class ThrowBehavior(int level) : IBehavior<IStage1Context, IStage1Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage1Context, Task> next)
    {
        await Console.Out.WriteLineAsync($"Enter ThrowBehavior {level}");
        throw new Exception();
    }
}

public sealed class LevelBehavior(int level) : IBehavior<IStage1Context, IStage1Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage1Context, Task> next)
    {
        await Console.Out.WriteLineAsync($"Enter Stage {level}");
        await next(context);
        await Console.Out.WriteLineAsync($"Exit Stage {level}");
    }
}

public sealed class Stage1Behavior : IBehavior<IStage1Context, IStage1Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage1Context, Task> next)
    {
        await Console.Out.WriteLineAsync("Enter Stage 1");
        await next(context);
        await Console.Out.WriteLineAsync("Exit Stage 1");
    }
}

public sealed class Stage1ToStage2Behavior : IBehavior<IStage1Context, IStage2Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage2Context, Task> next)
    {
        await Console.Out.WriteLineAsync("Enter Stage 1 to Stage 2");
        await next(new Stage2Context(context));
        await Console.Out.WriteLineAsync("Exit Stage 1 to Stage 2");
    }
}

public sealed class Stage2Behavior : IBehavior<IStage2Context, IStage2Context>
{
    public async Task Invoke(IStage2Context context, Func<IStage2Context, Task> next)
    {
        await Console.Out.WriteLineAsync("Enter Stage 2");
        await next(context);
        await Console.Out.WriteLineAsync("Exit Stage 2");
    }
}