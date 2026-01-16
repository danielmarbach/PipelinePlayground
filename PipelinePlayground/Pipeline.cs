using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PipelinePlayground;

public static class Pipeline
{
    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Stage1Behavior(IBehaviorContext ctx, int index, PipelinePart[] parts)
    {
        var context = Unsafe.As<Stage1Context>(ctx);
        var behavior = context.GetBehavior<Stage1Behavior>(index);
        return behavior.Invoke(context, Stage1CachedNext);
    }

    private static readonly Func<IStage1Context, Task> Stage1CachedNext = StageRunners.Next;

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Stage1ToStage2Behavior(IBehaviorContext ctx, int index, PipelinePart[] parts)
    {
        var context = Unsafe.As<Stage1Context>(ctx);
        var behavior = context.GetBehavior<Stage1ToStage2Behavior>(index);
        return behavior.Invoke(context, Stage2CachedNext);
    }

    [DebuggerStepThrough]
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Stage2Behavior(IBehaviorContext ctx, int index, PipelinePart[] parts)
    {
        var context = Unsafe.As<Stage2Context>(ctx);
        var behavior = context.GetBehavior<Stage2Behavior>(index);
        return behavior.Invoke(context, Stage2CachedNext);
    }

    private static readonly Func<IStage2Context, Task> Stage2CachedNext = StageRunners.Next;
}
