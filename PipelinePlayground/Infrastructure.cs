using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            Frame = parentContext.Frame;
        }
        else
        {
            Behaviors = [];
            Frame = new PipelineFrame();
        }
    }

    internal IBehavior[] Behaviors { get; init; }
    internal PipelineFrame Frame;

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TBehavior GetBehavior<TBehavior>(int index)
        where TBehavior : class, IBehavior
        => Unsafe.As<TBehavior>(
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Behaviors), index));
}

public readonly record struct FrameSnapshot(PipelinePart[] Parts, int Index);

[InlineArray(MaxDepth)]
public struct FrameStack
{
    public const int MaxDepth = 8; // this is well known

    private FrameSnapshot _element0;
}

[SkipLocalsInit]
public struct PipelineFrame
{
    public PipelinePart[] Parts = [];
    public int Index = 0;

    private FrameStack stack = default;
    private int stackDepth = 0;

    public PipelineFrame()
    {
    }

    // Should be verified whether those hints are still necessary
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(PipelinePart[] parts, int index)
    {
        var d = stackDepth;
        if ((uint)d >= FrameStack.MaxDepth)
        {
            ThrowOverflow();
        }

        stack[d] = new FrameSnapshot(parts, index);
        stackDepth = d + 1;
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [DebuggerHidden]
    // Should be verified whether those hints are still necessary
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out FrameSnapshot snapshot)
    {
        var d = stackDepth;
        if (d == 0)
        {
            snapshot = default;
            return false;
        }

        d--;
        snapshot = stack[d];
        stackDepth = d;
        return true;
    }

    [DoesNotReturn]
    private static void ThrowOverflow() => throw new InvalidOperationException($"Pipeline frame stack overflow. MaxDepth={FrameStack.MaxDepth}.");
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
        scoped ref var frame = ref context.Frame;
        frame.Parts = parts;
        frame.Index = 0;

        return parts.Length == 0 ? Complete(ctx) : Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(parts), 0).Invoke(ctx);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Next(IBehaviorContext ctx)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        scoped ref var frame = ref context.Frame;
        var parts = frame.Parts;
        var nextIndex = ++frame.Index;

        return (uint)nextIndex >= (uint)parts.Length ? Complete(ctx) : Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(parts), nextIndex).Invoke(ctx);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    static Task Complete(IBehaviorContext ctx)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        scoped ref var frame = ref context.Frame;
        if (!frame.TryPop(out var frameSnapshot))
        {
            return Task.CompletedTask;
        }

        frame.Parts = frameSnapshot.Parts;
        frame.Index = frameSnapshot.Index;

        return Next(ctx);
    }
}

public abstract class BehaviorPart<TContext, TBehavior>(int behaviorIndex) : PipelinePart
    where TContext : class, IBehaviorContext
    where TBehavior : class, IBehavior<TContext, TContext>
{
    private static readonly Func<TContext, Task> CachedNext = Next;

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override Task Invoke(IBehaviorContext context)
    {
        var ctx = (TContext)context;
        // In Core all this stuff is on extension and some of those casts are not necessary
        var behavior = Unsafe.As<BehaviorContext>(ctx).GetBehavior<TBehavior>(behaviorIndex);
        return behavior.Invoke(ctx, CachedNext);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task Next(TContext ctx) => StageRunners.Next(ctx);
}

// Given stages are backed into Core this logic could be moved into the corresponding stage connector base infrastucture
// and then we could safe another stack depth if needed.
public abstract class StagePart<TInContext, TOutContext, TBehavior>(int stageIndex, PipelinePart[] childParts) : PipelinePart
    where TInContext : class, IBehaviorContext
    where TOutContext : class, IBehaviorContext
    where TBehavior : class, IBehavior<TInContext, TOutContext>
{
    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    public sealed override Task Invoke(IBehaviorContext context)
    {
        var ctx = Unsafe.As<BehaviorContext>(context);
        scoped ref var frame = ref ctx.Frame;

        frame.Push(frame.Parts, frame.Index);

        frame.Parts = childParts;
        frame.Index = 0;

        return childParts.Length == 0
            ? StageRunners.Next(context)
            : ctx.GetBehavior<TBehavior>(stageIndex).Invoke(Unsafe.As<TInContext>(context), Start);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task Start(TOutContext ctx)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        scoped ref var frame = ref context.Frame;
        return StageRunners.Start(context, frame.Parts);
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
        throw  new Exception();
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