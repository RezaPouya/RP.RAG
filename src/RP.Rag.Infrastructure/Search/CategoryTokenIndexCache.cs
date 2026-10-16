using RP.RAG.Application.Search;

namespace RP.RAG.Infrastructure.Search;

public sealed class CategoryTokenIndexCache
{
    private volatile CategoryTokenIndex _index = CategoryTokenIndex.Empty;

    public CategoryTokenIndex Current => _index;

    public void Set(CategoryTokenIndex index) => _index = index;
}