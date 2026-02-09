using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PipelinePlayground;

public interface IBehavior;

public interface IBehaviorContext;

public class BehaviorContext : IBehaviorContext
{
    public BehaviorContext(IBehaviorContext? parent = null)
    {
        if (parent is BehaviorContext parentContext)
        {
            Behaviors = parentContext.Behaviors;
            Parts = parentContext.Parts;
            Frame = parentContext.Frame;
        }
        else
        {
            Behaviors = [];
            Parts = [];
            Frame = new PipelineFrame();
        }
    }

    internal IBehavior[] Behaviors { get; init; }
    internal PipelinePart[] Parts { get; init; }
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

public readonly record struct FrameSnapshot(int Index, int RangeEnd);

[InlineArray(MaxDepth)]
public struct FrameStack
{
    public const int MaxDepth = 8; // this is well known

    private FrameSnapshot _element0;
}

[SkipLocalsInit]
public struct PipelineFrame
{
    public int Index = 0;
    public int RangeEnd = 0;
    public int PendingChildStart = 0;
    public int PendingChildEnd = 0;

    private FrameStack stack = default;
    private int stackDepth = 0;

    public PipelineFrame()
    {
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(int index, int rangeEnd)
    {
        var d = stackDepth;
        if ((uint)d >= FrameStack.MaxDepth)
        {
            ThrowOverflow();
        }

        stack[d] = new FrameSnapshot(index, rangeEnd);
        stackDepth = d + 1;
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [DebuggerHidden]
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

public readonly record struct PipelinePart(Func<IBehaviorContext, int, int, Task> Invoke, int ChildStart = 0, int ChildEnd = 0);

public static class StageRunners
{
    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Start(IBehaviorContext ctx)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        return Start(ctx, startIndex: 0, rangeEnd: context.Parts.Length);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Start(IBehaviorContext ctx, int startIndex, int rangeEnd)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        scoped ref var frame = ref context.Frame;
        frame.Index = startIndex;
        frame.RangeEnd = rangeEnd;

        if (startIndex >= rangeEnd)
        {
            return Complete(ctx);
        }

        var parts = context.Parts;
        scoped ref var part = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(parts), startIndex);
        return part.Invoke(ctx, part.ChildStart, part.ChildEnd);
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
        var parts = context.Parts;
        var nextIndex = ++frame.Index;

        if ((uint)nextIndex >= (uint)frame.RangeEnd)
        {
            return Complete(ctx);
        }

        scoped ref var part = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(parts), nextIndex);
        return part.Invoke(ctx, part.ChildStart, part.ChildEnd);
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

        frame.Index = frameSnapshot.Index;
        frame.RangeEnd = frameSnapshot.RangeEnd;

        return Next(ctx);
    }
}

public static class BehaviorPartFactory
{
    private static class Cache<TContext, TBehavior>
        where TContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TContext, TContext>
    {
        // Cached delegate to avoid allocation on every invocation
        private static readonly Func<TContext, Task> Next = static ctx => StageRunners.Next(ctx);

        public static readonly Func<IBehaviorContext, int, int, Task> Invoke =
            static (ctx, _, _) =>
            {
                var context = Unsafe.As<BehaviorContext>(ctx);
                scoped ref var frame = ref context.Frame;
                var behavior = context.GetBehavior<TBehavior>(frame.Index);
                return behavior.Invoke(Unsafe.As<TContext>(ctx), Next);
            };
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    public static PipelinePart Create<TContext, TBehavior>()
        where TContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TContext, TContext>
        => new(Cache<TContext, TBehavior>.Invoke);
}

public static class StagePartFactory
{
    private static class Cache<TInContext, TOutContext, TBehavior>
        where TInContext : class, IBehaviorContext
        where TOutContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TInContext, TOutContext>
    {
        public static readonly Func<IBehaviorContext, int, int, Task> Invoke =
            static (ctx, childStart, childEnd) =>
            {
                var context = Unsafe.As<BehaviorContext>(ctx);
                scoped ref var frame = ref context.Frame;

                frame.Push(frame.Index, frame.RangeEnd);
                frame.PendingChildStart = childStart;
                frame.PendingChildEnd = childEnd;

                return context.GetBehavior<TBehavior>(frame.Index).Invoke(Unsafe.As<TInContext>(ctx), Start);
            };
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    public static PipelinePart Create<TInContext, TOutContext, TBehavior>(int childStartIndex, int childEndIndex)
        where TInContext : class, IBehaviorContext
        where TOutContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TInContext, TOutContext>
        => new(Cache<TInContext, TOutContext, TBehavior>.Invoke, childStartIndex, childEndIndex);

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task Start<TOutContext>(TOutContext ctx)
        where TOutContext : class, IBehaviorContext
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        scoped ref var frame = ref context.Frame;
        return StageRunners.Start(context, frame.PendingChildStart, frame.PendingChildEnd);
    }
}

public interface IStage1Context : IBehaviorContext;

class Stage1Context : BehaviorContext, IStage1Context;

public interface IStage2Context : IBehaviorContext;

class Stage2Context(IStage1Context context) : BehaviorContext(context), IStage2Context;

public interface IBehavior<in TInContext, out TOutContext> : IBehavior
    where TInContext : IBehaviorContext
{
    Task Invoke(TInContext context, Func<TOutContext, Task> next);
}

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