namespace RP.RAG.Application.Ask;

public interface IAnswerGeneratorResolver
{
    IAnswerGenerator Resolve();
}