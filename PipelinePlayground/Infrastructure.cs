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
    internal IBehavior GetBehavior() =>
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Behaviors), Frame.Index);
}

public struct PipelineFrame
{
    public int Index = 0;
    public int RangeEnd = 0;

    public PipelineFrame()
    {
    }
}

readonly record struct PipelinePart(
    byte InvokerId,
    Func<IBehaviorContext, int, int, Task>? FallbackInvoke,
    int ChildStart,
    int ChildEnd);

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
        ref var frame = ref context.Frame;
        frame.Index = 0;
        frame.RangeEnd = context.Parts.Length;

        return context.Parts.Length == 0 ? Task.CompletedTask : Dispatch(ctx, 0);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Next(IBehaviorContext ctx)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        ref var frame = ref context.Frame;
        var nextIndex = ++frame.Index;

        if ((uint)nextIndex >= (uint)frame.RangeEnd)
        {
            return Task.CompletedTask;
        }

        return Dispatch(ctx, nextIndex);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Task Dispatch(IBehaviorContext ctx, int index)
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        ref var part = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(context.Parts), index);
        return KnownPipelineInvokers.Invoke(ctx, part);
    }
}

static class BehaviorPartFactory
{
    static class Cache<TContext, TBehavior>
        where TContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TContext, TContext>
    {
        public static readonly Func<IBehaviorContext, int, int, Task> Invoke =
            static (ctx, _, _) =>
            {
                var context = Unsafe.As<BehaviorContext>(ctx);
                var behavior = Unsafe.As<TBehavior>(context.GetBehavior());
                return behavior.Invoke(Unsafe.As<TContext>(ctx), Start!);
            };

        static readonly Func<TContext, Task> Start = StageRunners.Next;
    }

    [DebuggerNonUserCode]
    [DebuggerHidden]
    [DebuggerStepThrough]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PipelinePart Create<TContext, TBehavior>()
        where TContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TContext, TContext>
    {
        var invokerId = PipelinePartInvokerIds.GetBehaviorId(typeof(TContext));
        var fallback = invokerId == PipelinePartInvokerIds.Fallback ? Cache<TContext, TBehavior>.Invoke : null;
        return new(invokerId, fallback, 0, 0);
    }
}

static class StagePartFactory
{
    static class Cache<TInContext, TOutContext, TBehavior>
        where TInContext : class, IBehaviorContext
        where TOutContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TInContext, TOutContext>
    {
        public static readonly Func<IBehaviorContext, int, int, Task> Invoke =
            static (ctx, childStart, childEnd) =>
            {
                var context = Unsafe.As<BehaviorContext>(ctx);
                ref var frame = ref context.Frame;

                frame.Index = childStart - 1;
                frame.RangeEnd = childEnd;

                var behavior = Unsafe.As<TBehavior>(context.GetBehavior());
                return behavior.Invoke(Unsafe.As<TInContext>(ctx), Start!);
            };

        static readonly Func<TOutContext, Task> Start = StageRunners.Next;
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    public static PipelinePart Create<TInContext, TOutContext, TBehavior>(int childStartIndex, int childEndIndex)
        where TInContext : class, IBehaviorContext
        where TOutContext : class, IBehaviorContext
        where TBehavior : class, IBehavior<TInContext, TOutContext>
    {
        var invokerId = PipelinePartInvokerIds.GetStageId(typeof(TInContext), typeof(TOutContext));
        var fallback = invokerId == PipelinePartInvokerIds.Fallback ? Cache<TInContext, TOutContext, TBehavior>.Invoke : null;
        return new(invokerId, fallback, childStartIndex, childEndIndex);
    }
}

static class KnownPipelineInvokers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Invoke(IBehaviorContext ctx, in PipelinePart part)
    {
        return part.InvokerId switch
        {
            PipelinePartInvokerIds.BehaviorStage1 => InvokeBehavior<IStage1Context>(ctx),
            PipelinePartInvokerIds.BehaviorStage2 => InvokeBehavior<IStage2Context>(ctx),

            PipelinePartInvokerIds.Stage1ToStage2 => InvokeStage<IStage1Context, IStage2Context>(ctx, part.ChildStart, part.ChildEnd),

            _ => InvokeFallback(ctx, part)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Task InvokeBehavior<TContext>(IBehaviorContext ctx)
        where TContext : class, IBehaviorContext
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        var behavior = Unsafe.As<IBehavior<TContext, TContext>>(context.GetBehavior());
        return behavior.Invoke(Unsafe.As<TContext>(ctx), BehaviorNextCache<TContext>.Next);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Task InvokeStage<TInContext, TOutContext>(IBehaviorContext ctx, int childStart, int childEnd)
        where TInContext : class, IBehaviorContext
        where TOutContext : class, IBehaviorContext
    {
        var context = Unsafe.As<BehaviorContext>(ctx);
        ref var frame = ref context.Frame;
        frame.Index = childStart - 1;
        frame.RangeEnd = childEnd;

        var behavior = Unsafe.As<IBehavior<TInContext, TOutContext>>(context.GetBehavior());
        return behavior.Invoke(Unsafe.As<TInContext>(ctx), StageNextCache<TOutContext>.Next);
    }

    [DoesNotReturn]
    static Task InvokeFallback(IBehaviorContext ctx, in PipelinePart part)
    {
        // if (part.FallbackInvoke != null)
        // {
        //     return part.FallbackInvoke(ctx, part.ChildStart, part.ChildEnd);
        // }

        throw new InvalidOperationException($"Unknown invoker id '{part.InvokerId}' and no fallback delegate was provided.");
    }

    static class BehaviorNextCache<TContext> where TContext : class, IBehaviorContext
    {
        public static readonly Func<TContext, Task> Next = StageRunners.Next;
    }

    static class StageNextCache<TOutContext> where TOutContext : class, IBehaviorContext
    {
        public static readonly Func<TOutContext, Task> Next = StageRunners.Next;
    }
}

static class PipelinePartInvokerIds
{
    public const byte Fallback = 0;

    public const byte BehaviorStage1 = 1;
    public const byte BehaviorStage2 = 2;

    public const byte Stage1ToStage2 = 101;

    public static byte GetBehaviorId(Type contextType)
    {
        if (contextType == typeof(IStage1Context))
        {
            return BehaviorStage1;
        }

        if (contextType == typeof(IStage2Context))
        {
            return BehaviorStage2;
        }

        return Fallback;
    }

    public static byte GetStageId(Type inContextType, Type outContextType)
    {
        if (inContextType == typeof(IStage1Context) && outContextType == typeof(IStage2Context))
        {
            return Stage1ToStage2;
        }

        return Fallback;
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