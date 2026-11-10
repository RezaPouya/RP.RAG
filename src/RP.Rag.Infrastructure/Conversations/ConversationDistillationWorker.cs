using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RP.Rag.Application.Conversations;
using RP.Rag.Infrastructure.Data;

namespace RP.Rag.Infrastructure.Conversations;

public sealed class ConversationDistillationWorker(IServiceScopeFactory scopeFactory, IOptions<ConversationOptions> options, ILogger<ConversationDistillationWorker> logger) : BackgroundService
{
    private readonly ConversationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ConversationDistillationWorker started. Polling every 15 seconds.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RpRagDbContext>();
                var memoryService = scope.ServiceProvider.GetRequiredService<IConversationMemoryService>();

                // Find conversations that have enough new messages to trigger distillation
                var pendingIds = await db.Conversations
                    .Where(c => c.MessageCount - c.DistilledThroughSequence >= _options.DistillAfterMessageCount)
                    .Select(c => c.Id)
                    .ToListAsync(stoppingToken);

                if (pendingIds.Count > 0)
                {
                    logger.LogInformation("Distilling {Count} conversations in background.", pendingIds.Count);
                }

                foreach (var id in pendingIds)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        // Reuses your existing LLM extraction logic safely in the background
                        await memoryService.DistillIfNeededAsync(id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to distill conversation {ConversationId}.", id);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Conversation distillation cycle failed.");
            }

            // Wait 15 seconds before checking the database again
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}