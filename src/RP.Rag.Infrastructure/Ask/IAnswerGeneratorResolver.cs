using RP.RAG.Application.Ask;

namespace RP.Rag.Infrastructure.Ask;

public interface IAnswerGeneratorResolver
{
    IAnswerGenerator Resolve();
}