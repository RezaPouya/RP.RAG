using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RP.Rag.Domain.Entities.Products;
using System.Runtime.CompilerServices;

namespace RP.RAG.Infrastructure.Data.Interceptors;

// Flags a product for re-indexing when its searchable content changes.
// It only collects ids while saving and updates ProductSync after the save succeeded,
// with one set-based UPDATE. It never queries or saves from inside SaveChanges.
// Register it once as a singleton: a new instance per DbContext makes EF build
// a new internal service provider every time.
public sealed class ProductSyncInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> ContentProperties = new()
    {
        nameof(Product.Name),
        nameof(Product.Summary),
        nameof(Product.Description),
        nameof(Product.SKU),
        nameof(Product.ProductCategoryId),
        nameof(Product.IsActive)
    };

    private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var ids = Resolve(eventData.Context);

        if (ids.Count > 0)
        {
            Flag(eventData.Context!, ids)
                .ExecuteUpdate(Apply(DateTime.UtcNow));
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var ids = Resolve(eventData.Context);

        if (ids.Count > 0)
        {
            await Flag(eventData.Context!, ids)
                .ExecuteUpdateAsync(Apply(DateTime.UtcNow), cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Discard(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Collect(DbContext? context)
    {
        if (context is null)
            return;

        var pending = _pending.GetOrCreateValue(context);
        pending.Clear();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case Product product when entry.State == EntityState.Modified && HasContentChange(entry):
                    pending.ProductIds.Add(product.Id);
                    break;

                case ProductTag tag when entry.State == EntityState.Deleted:
                    pending.ProductIds.Add(tag.ProductId);
                    break;

                case ProductTag tag when entry.State == EntityState.Added:
                    pending.Added.Add(tag);
                    break;

                case ProductDocument doc when entry.State is EntityState.Modified or EntityState.Deleted:
                    pending.ProductIds.Add(doc.ProductId);
                    break;

                case ProductDocument doc when entry.State == EntityState.Added:
                    pending.Added.Add(doc);
                    break;
            }
        }
    }

    // Tags and documents added together with a new product only get their ProductId
    // after the save. Products without a ProductSync row yet are simply not matched.
    private List<int> Resolve(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
            return [];

        foreach (var added in pending.Added)
        {
            switch (added)
            {
                case ProductTag tag:
                    pending.ProductIds.Add(tag.ProductId);
                    break;
                case ProductDocument doc:
                    pending.ProductIds.Add(doc.ProductId);
                    break;
            }
        }

        var ids = pending.ProductIds.Where(id => id > 0).ToList();
        pending.Clear();

        return ids;
    }

    private void Discard(DbContext? context)
    {
        if (context is not null && _pending.TryGetValue(context, out var pending))
            pending.Clear();
    }

    private static IQueryable<ProductSync> Flag(DbContext context, List<int> productIds)
        => context.Set<ProductSync>().Where(s => productIds.Contains(s.ProductId));

    private static Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<ProductSync>> Apply(DateTime now)
        => set => set
            .SetProperty(s => s.ContentChanged, true)
            .SetProperty(s => s.EmbeddingChanged, false)
            .SetProperty(s => s.ContentSyncRetries, 0)
            .SetProperty(s => s.EmbeddingSyncRetries, 0)
            .SetProperty(s => s.UpdatedAt, now);

    private static bool HasContentChange(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (property.IsModified && ContentProperties.Contains(property.Metadata.Name))
                return true;
        }

        return false;
    }

    private sealed class Pending
    {
        public HashSet<int> ProductIds { get; } = new();
        public List<object> Added { get; } = new();

        public void Clear()
        {
            ProductIds.Clear();
            Added.Clear();
        }
    }
}
