using DevFlow.Services;

namespace DevFlow.Models.Operations;

public interface IOperation
{
    string Description { get; }
    void Undo();
    void Redo();
    void LogDetails(ILogService logger);
}
