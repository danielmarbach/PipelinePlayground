using System.Collections;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace PipelinePlayground;

public sealed class Stage1BehaviorPart(int behaviorIndex) : BehaviorPart<IStage1Context, Stage1Behavior>(behaviorIndex);

public sealed class Stage2BehaviorPart(int behaviorIndex) : BehaviorPart<IStage2Context, Stage2Behavior>(behaviorIndex);

public sealed class Stage1ToStage2BehaviorPart(PipelinePart[] childParts) : PipelinePart
{
    public override Task Invoke(IBehaviorContext context)
    {
        var frame = context.Frame;

        frame.Parent = new PipelineFrameSnapshot
        {
            Parts = frame.Parts,
            Index = frame.Index,
            Parent = frame.Parent
        };

        frame.Parts = childParts;
        frame.Index = 0;

        var nextContext = new Stage2Context
        {
            Behaviors = ((BehaviorContext)context).Behaviors
        };

        return childParts.Length == 0
            ? StageRunners.Next(nextContext)
            : childParts[0].Invoke(nextContext);
    }
}