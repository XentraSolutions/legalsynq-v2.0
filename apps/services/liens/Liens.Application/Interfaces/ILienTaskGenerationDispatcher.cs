namespace Liens.Application.Interfaces;

public interface ILienTaskGenerationDispatcher
{
    void Dispatch(TaskGenerationContext context);
}
