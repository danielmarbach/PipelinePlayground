namespace PipelinePlayground;

public interface IBehavior;

public interface IBehaviorContext;

public interface IBehavior<in TInContext, out TOutContext> : IBehavior
    where TInContext : IBehaviorContext
{
    Task Invoke(TInContext context, Func<TOutContext, Task> next);
}

interface IStage1Context : IBehaviorContext;

class Stage1Context : IStage1Context;

interface IStage2Context : IBehaviorContext;

class Stage2Context : IStage2Context;

class Stage1Behavior : IBehavior<IStage1Context, IStage1Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage1Context, Task> next)
    {
        await next(context);
    }
}

class Stage1ToStage2Behavior : IBehavior<IStage1Context, IStage2Context>
{
    public async Task Invoke(IStage1Context context, Func<IStage2Context, Task> next)
    {
        await next(new Stage2Context());
    }
}

class Stage2Behavior : IBehavior<IStage2Context, IStage2Context>
{
    public async Task Invoke(IStage2Context context, Func<IStage2Context, Task> next)
    {
        await next(context);
    }
}