namespace PipelinePlayground;

public sealed class Stage1BehaviorPart(int behaviorIndex) : BehaviorPart<IStage1Context, Stage1Behavior>(behaviorIndex);

public sealed class Stage2BehaviorPart(int behaviorIndex) : BehaviorPart<IStage2Context, Stage2Behavior>(behaviorIndex);

public sealed class Stage1ToStage2BehaviorPart(int stageIndex, PipelinePart[] childParts) : StagePart<IStage1Context, IStage2Context, Stage1ToStage2Behavior>(stageIndex, childParts);