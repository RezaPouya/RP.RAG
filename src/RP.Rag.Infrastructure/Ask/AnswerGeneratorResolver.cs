using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.RAG.Application.Ask;

namespace RP.RAG.Infrastructure.Ask;

public sealed class AnswerGeneratorResolver(
    IServiceProvider serviceProvider,
    IOptions<GenerationOptions> options,
    ILogger<AnswerGeneratorResolver> logger) : IAnswerGeneratorResolver
{
    private readonly GenerationOptions _options = options.Value;

    public IAnswerGenerator Resolve()
    {
        var provider = _options.Provider ?? "Ollama";

        var key = provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase)
            ? ServiceKeys.DeepSeek
            : ServiceKeys.Ollama;

        logger.LogDebug("Resolving answer generator: Provider={Provider}, Key={Key}", provider, key);

        return serviceProvider.GetRequiredKeyedService<IAnswerGenerator>(key);
    }
}