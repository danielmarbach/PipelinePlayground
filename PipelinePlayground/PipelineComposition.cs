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
            parts[i] = BehaviorPartFactory.Create<IStage1Context, LevelBehavior>(i);
        }

        if (@throw)
        {
            behaviors[depth] = new ThrowBehavior(depth);
            parts[depth] = BehaviorPartFactory.Create<IStage1Context, ThrowBehavior>(depth);
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

        var stage2Parts = new[]
        {
            BehaviorPartFactory.Create<IStage2Context, Stage2Behavior>(behaviorIndex: 2)
        };

        var stage1Parts = new[]
        {
            BehaviorPartFactory.Create<IStage1Context, Stage1Behavior>(behaviorIndex: 0),
            StagePartFactory.Create<IStage1Context, IStage2Context, Stage1ToStage2Behavior>(stageIndex: 1, stage2Parts)
        };

        var ctx = new Stage1Context
        {
            Behaviors = behaviors
        };
        await StageRunners.Start(ctx, stage1Parts);
    }
}