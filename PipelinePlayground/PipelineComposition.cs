using NUnit.Framework;

namespace PipelinePlayground;

[TestFixture]
class PipelineTests
{
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