using System.Collections;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace PipelinePlayground;

// Can give richer information about the pipeline part
public abstract class PipelinePartDescriptor
{
    public abstract void Initialize(IServiceProvider serviceProvider);

    public abstract Task Invoke(IBehaviorContext context, Func<IBehaviorContext, Task> next);
}

// For reflection stuff
// Source can generate these
class PipelinePartDescriptor<TBehavior>(Func<IBehaviorContext, Func<IBehaviorContext, Task>, Task> invoker) : PipelinePartDescriptor
    where TBehavior : IBehavior
{
    private TBehavior? behavior;

    public override void Initialize(IServiceProvider serviceProvider)
    {
        // would use activator utilities in real implementation
        behavior = (TBehavior)serviceProvider.GetRequiredService(typeof(TBehavior));
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [DebuggerNonUserCode]
    [DebuggerHidden]
    public override Task Invoke(IBehaviorContext context, Func<IBehaviorContext, Task> next)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        return invoker(context, next);
    }
}


class PipelinePart1 : IEnumerable<PipelinePartDescriptor>
{
    public IEnumerator<PipelinePartDescriptor> GetEnumerator()
    {
        yield return new Stage1PipelinePartDescriptor();
        yield return new Stage1ToStage2BehaviorPartDescriptor();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    [DebuggerStepThrough]
    [DebuggerNonUserCode]
    sealed class Stage1PipelinePartDescriptor : PipelinePartDescriptor
    {
        Stage1Behavior? behavior;

        public override void Initialize(IServiceProvider serviceProvider)
        {
            behavior = serviceProvider.GetRequiredService<Stage1Behavior>();
        }

        [StackTraceHidden]
        [DebuggerStepThrough]
        [DebuggerNonUserCode]
        public override Task Invoke(IBehaviorContext context, Func<IBehaviorContext, Task> next)
        {
            ArgumentNullException.ThrowIfNull(behavior);
            return behavior.Invoke((IStage1Context)context, next);
        }
    }

    [DebuggerStepThrough]
    [DebuggerNonUserCode]
    sealed class Stage1ToStage2BehaviorPartDescriptor : PipelinePartDescriptor
    {
        Stage1ToStage2Behavior? behavior;

        public override void Initialize(IServiceProvider serviceProvider)
        {
            behavior = serviceProvider.GetRequiredService<Stage1ToStage2Behavior>();
        }

        [StackTraceHidden]
        [DebuggerStepThrough]
        [DebuggerNonUserCode]
        public override Task Invoke(IBehaviorContext context, Func<IBehaviorContext, Task> next)
        {
            ArgumentNullException.ThrowIfNull(behavior);
            return behavior.Invoke((IStage1Context)context, next);
        }
    }
}

class PipelinePart2 : IEnumerable<PipelinePartDescriptor>
{
    public IEnumerator<PipelinePartDescriptor> GetEnumerator()
    {
        // ordering within the same assembly can be guaranteed by the order here potentially or be returned by returning a richer object
        yield return new Stage2PipelinePartDescriptor();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    [DebuggerStepThrough]
    [DebuggerNonUserCode]
    sealed class Stage2PipelinePartDescriptor : PipelinePartDescriptor
    {
        Stage2Behavior? behavior;

        public override void Initialize(IServiceProvider serviceProvider)
        {
            behavior = serviceProvider.GetRequiredService<Stage2Behavior>();
        }

        [StackTraceHidden]
        public override Task Invoke(IBehaviorContext context, Func<IBehaviorContext, Task> next)
        {
            ArgumentNullException.ThrowIfNull(behavior);
            return behavior.Invoke((IStage2Context)context, next);
        }
    }
}