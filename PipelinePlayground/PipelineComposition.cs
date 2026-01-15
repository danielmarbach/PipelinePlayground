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
            parts[i] = new LevelBehaviorPart(i);
        }

        if (@throw)
        {
            behaviors[depth] = new ThrowBehavior(depth);
            parts[depth] = new ThrowBehaviorPart(depth);
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

        var stage2Parts = new PipelinePart[]
        {
            new Stage2BehaviorPart(behaviorIndex: 2)
        };

        var stage1Parts = new PipelinePart[]
        {
            new Stage1BehaviorPart(behaviorIndex: 0),
            new Stage1ToStage2BehaviorPart(stageIndex: 1, stage2Parts),
        };

        var ctx = new Stage1Context
        {
            Behaviors = behaviors
        };
        await StageRunners.Start(ctx, stage1Parts);
    }
}