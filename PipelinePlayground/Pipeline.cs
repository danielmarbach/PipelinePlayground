namespace PipelinePlayground;

public sealed class ThrowBehaviorPart : BehaviorPart<IStage1Context, ThrowBehavior>;
public sealed class LevelBehaviorPart : BehaviorPart<IStage1Context, LevelBehavior>;

public sealed class Stage1BehaviorPart : BehaviorPart<IStage1Context, Stage1Behavior>;

public sealed class Stage2BehaviorPart : BehaviorPart<IStage2Context, Stage2Behavior>;

public sealed class Stage1ToStage2BehaviorPart(int nextStageStartIndex) : StagePart<IStage1Context, IStage2Context, Stage1ToStage2Behavior>(nextStageStartIndex);
