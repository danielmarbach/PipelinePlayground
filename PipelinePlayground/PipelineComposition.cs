using NUnit.Framework;

namespace PipelinePlayground;

[TestFixture]
class PipelineTests
{
    [Test]
    [TestCase(20, false)]
    [TestCase(20, true)]
    [TestCase(40, false)]
    [TestCase(40, true)]
    public async Task Depth(int depth, bool @throw)
    {
        var behaviors = new IBehavior[depth + (@throw ? 1 : 0)];
        var parts = new PipelinePart[depth + (@throw ? 1 : 0)];

        for (var i = 0; i < depth; i++)
        {
            behaviors[i] = new LevelBehavior(i);
            parts[i] = new PipelinePart(BehaviorPart<IStage1Context, LevelBehavior>.Invoke);
        }

        if (@throw)
        {
            behaviors[depth] = new ThrowBehavior(depth);
            parts[depth] = new PipelinePart(BehaviorPart<IStage1Context, ThrowBehavior>.Invoke);
        }

        var ctx = new Stage1Context
        {
            Behaviors = behaviors
        };
        await StageRunners.Start(ctx, parts);
    }

    [Test]
    public async Task Foo()
    {
        var behaviors = new IBehavior[]
        {
            new Stage1Behavior(),         // index 0
            new Stage1ToStage2Behavior(), // index 1
            new Stage2Behavior()          // index 2
        };

        var parts = new[]
        {
            new PipelinePart(Pipeline.Stage1Behavior),
            new PipelinePart(Pipeline.Stage1ToStage2Behavior, NextStageStartIndex: 2),
            new PipelinePart(Pipeline.Stage2Behavior),
        };

        var ctx = new Stage1Context
        {
            Behaviors = behaviors
        };
        await StageRunners.Start(ctx, parts);
    }
}