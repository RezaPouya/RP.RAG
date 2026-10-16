using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RP.Rag.Infrastructure.Data;
using RP.RAG.Application.Search;

namespace RP.RAG.Infrastructure.Elastic;

public sealed class CategoryTokenIndexBuilder(
    IDbContextFactory<RpRagDbContext> dbFactory,
    ILogger<CategoryTokenIndexBuilder> logger)
{
    private const int MinProductsPerCategory = 3;
    private const int MinProductsContainingToken = 3;
    private const double MinCategoryShare = 0.05;
    private const double MinLeadRatio = 1.5;

    private static readonly string[] ExcludedCategories =
    [
        "Unknown",
        "Misc"
    ];

    public async Task<CategoryTokenIndex> BuildAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new
            {
                CategoryName = p.Category != null ? p.Category.Name : null,
                p.Name,
                p.Summary,
                p.Description,
                Tags = p.ProductTags.Select(pt => pt.Tag.Name).ToList()
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            logger.LogWarning("Category token index: no products found.");
            return CategoryTokenIndex.Empty;
        }

        var perCategoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var perCategoryTokenCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var category = row.CategoryName;

            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            if (ExcludedCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            perCategoryCounts[category] = perCategoryCounts.GetValueOrDefault(category) + 1;

            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddTokens(tokens, row.Name);
            AddTokens(tokens, row.Summary);
            AddTokens(tokens, row.Description);

            foreach (var tag in row.Tags)
            {
                AddTokens(tokens, tag);
            }

            if (!perCategoryTokenCounts.TryGetValue(category, out var tokenCounts))
            {
                tokenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                perCategoryTokenCounts[category] = tokenCounts;
            }

            foreach (var token in tokens)
            {
                tokenCounts[token] = tokenCounts.GetValueOrDefault(token) + 1;
            }
        }

        // Compute, for every (token), the highest per-category product count.
        // Then, for each category, keep tokens that:
        //  - appear in at least MinProductsContainingToken products of that category
        //  - cover at least MinCategoryShare of that category's products
        //  - are at least MinLeadRatio times more concentrated here than in any other category

        var filtered = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, tokenCounts) in perCategoryTokenCounts)
        {
            var categoryProductCount = perCategoryCounts[category];

            if (categoryProductCount < MinProductsPerCategory)
            {
                logger.LogDebug(
                    "Category '{Category}' skipped: only {Count} products (min={Min}).",
                    category,
                    categoryProductCount,
                    MinProductsPerCategory);

                continue;
            }

            var normalized = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var (token, countInCategory) in tokenCounts)
            {
                if (countInCategory < MinProductsContainingToken)
                {
                    continue;
                }

                var shareInCategory = (double)countInCategory / categoryProductCount;

                if (shareInCategory < MinCategoryShare)
                {
                    continue;
                }

                // Find the highest count of this token in any OTHER category.
                var maxInOtherCategory = 0;

                foreach (var (otherCategory, otherTokenCounts) in perCategoryTokenCounts)
                {
                    if (string.Equals(otherCategory, category, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (otherTokenCounts.TryGetValue(token, out var otherCount))
                    {
                        if (otherCount > maxInOtherCategory)
                        {
                            maxInOtherCategory = otherCount;
                        }
                    }
                }

                // Lead ratio: how much more concentrated is this token here
                // than anywhere else?
                if (maxInOtherCategory > 0 && countInCategory < maxInOtherCategory * MinLeadRatio)
                {
                    continue;
                }

                normalized[token] = shareInCategory;
            }

            if (normalized.Count > 0)
            {
                filtered[category] = normalized;
            }
        }

        logger.LogInformation(
            "Category token index built: {Categories} categories, {TotalTokens} tokens.",
            filtered.Count,
            filtered.Values.Sum(w => w.Count));

        return CategoryTokenIndex.Create(filtered);
    }

    private static void AddTokens(HashSet<string> tokens, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var token in CategoryTokenIndex.Tokenize(text))
        {
            tokens.Add(token);
        }
    }
}