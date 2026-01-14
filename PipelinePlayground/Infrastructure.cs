using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PipelinePlayground;

public interface IBehavior;

public interface IBehaviorContext
{
    // In Core this would move to extensions and not be exposed on the interface to public
    PipelineFrame Frame { get; }
}

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
    public PipelineFrame Frame { get; }

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

public sealed class PipelineFrame
{
    public PipelinePart[] Parts = [];
    public int Index;

    private FrameStack stack;
    private int stackDepth;

    // Should be verified whether those hints are still necessary
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
    public static Task Start(IBehaviorContext ctx, PipelinePart[] parts)
    {
        var frame = ctx.Frame;
        frame.Parts = parts;
        frame.Index = 0;

        return parts.Length == 0 ? Complete(ctx) : Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(parts), 0).Invoke(ctx);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    public static Task Next(IBehaviorContext ctx)
    {
        var f = ctx.Frame;
        var nextIndex = ++f.Index;

        return (uint)nextIndex < (uint)f.Parts.Length ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(f.Parts), nextIndex).Invoke(ctx) : Complete(ctx);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    static Task Complete(IBehaviorContext ctx)
    {
        var frame = ctx.Frame;
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
    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    public sealed override Task Invoke(IBehaviorContext context)
    {
        var ctx = (TContext)context;
        // In Core all this stuff is on extension and some of those casts are not necessary
        var behavior = (ctx as BehaviorContext)!.GetBehavior<TBehavior>(behaviorIndex);
        return behavior.Invoke(ctx, static ctx => StageRunners.Next(ctx));
    }
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
        var frame = context.Frame;

        frame.Push(frame.Parts, frame.Index);

        frame.Parts = childParts;
        frame.Index = 0;

        return childParts.Length == 0
            ? StageRunners.Next(context)
            : (context as BehaviorContext)!.GetBehavior<TBehavior>(stageIndex).Invoke((TInContext)context, static ctx => StageRunners.Start(ctx, ctx.Frame.Parts));
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