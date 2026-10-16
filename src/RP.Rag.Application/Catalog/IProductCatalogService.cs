namespace RP.RAG.Application.Catalog;

public interface IProductCatalogService
{
    Task<IReadOnlyDictionary<int, ProductCard>> GetCardsAsync(
        IReadOnlyCollection<int> productIds,
        bool onlyActive = true,
        CancellationToken ct = default);
}
