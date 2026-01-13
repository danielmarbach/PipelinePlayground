using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace PipelinePlayground;

public class PipelineComposition(PipelinePartDescriptor[] descriptors)
{
    public Func<IBehaviorContext, Func<IBehaviorContext, Task>, Task>? Compose()
    {
        Func<IBehaviorContext, Func<IBehaviorContext, Task>, Task>? previous = null;
        for (var i = descriptors.Length - 1; i >= 0; i--)
        {
            Func<IBehaviorContext, Func<IBehaviorContext, Task>, Task>? current;
            if (previous is not null)
            {
                var i0 = i;
                var previous1 = previous;

                current = CurrentWithPrevious; // closure :(
                previous = current;
                continue;

                [StackTraceHidden]
                [DebuggerStepThrough]
                [DebuggerNonUserCode]
                [DebuggerHidden]
                Task CurrentWithPrevious(IBehaviorContext context, Func<IBehaviorContext, Task> func) => descriptors[i0].Invoke(context, (ctx) => previous1(ctx, func));
            }

            var i1 = i;

            current = Current; // closure :(
            previous = current;
            continue;

            [StackTraceHidden]
            [DebuggerStepThrough]
            [DebuggerNonUserCode]
            [DebuggerHidden]
            Task Current(IBehaviorContext context, Func<IBehaviorContext, Task> func) => descriptors[i1].Invoke(context, func);
        }

        return previous;
    }
}

[TestFixture]
class PipelineTests
{
    [Test]
    public async Task Foo()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<Stage1Behavior>();
        serviceCollection.AddSingleton<Stage2Behavior>();
        serviceCollection.AddSingleton<Stage1ToStage2Behavior>();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        PipelinePartDescriptor[] pipelinePartDescriptors = [..new PipelinePart1().Union(new PipelinePart2())];
        foreach (var descriptor in pipelinePartDescriptors)
        {
            descriptor.Initialize(serviceProvider);
        }
        var composition = new PipelineComposition(pipelinePartDescriptors);
        var invoker = composition.Compose();
        await invoker(new Stage1Context(), _ => Task.CompletedTask);
    }
}