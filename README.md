# RP.RAG

**RP.RAG** is a small, end-to-end Retrieval-Augmented Generation (RAG)
backend for an online product catalog. It is designed as a readable
reference implementation for exploring lexical search, vector-based
semantic search, hybrid retrieval, and grounded answers in a .NET
application.

The project uses **.NET 10**, **PostgreSQL + pgvector**,
**Elasticsearch**, and **Ollama**. Answer generation can also be
configured to use **DeepSeek**.

> This is a learning/reference project, not a production-ready search
> platform. Search relevance, prompt-injection resistance, operational
> security, and model output quality must be evaluated for the target
> catalog and deployment.

## What it demonstrates

-   **Lexical retrieval** with Elasticsearch, including English and
    Persian text analysis.
-   **Semantic retrieval** by embedding product content and querying
    pgvector using cosine distance.
-   **Hybrid retrieval** that combines lexical and semantic ranks using
    Reciprocal Rank Fusion (RRF).
-   **Chunk-level retrieval** across product summaries, descriptions,
    and attached product documents such as warranties, return policies,
    manuals, specifications, and FAQs.
-   **Grounded answer generation** using retrieved product data and a
    configurable generation provider.
-   **Database-backed product cards and source references**: product
    facts returned by the API are loaded from PostgreSQL rather than
    invented by the language model.
-   **Background indexing and embedding workers** that track changes and
    retry failed processing.
-   **Seed data and labeled search queries** for repeatable local
    experiments.

## Architecture

``` text
                         ┌─────────────────────┐
                         │   Seed JSON files   │
                         └──────────┬──────────┘
                                    ▼
                         ┌─────────────────────┐
                         │     PostgreSQL      │
                         │ Products, tags,     │
                         │ documents, chunks,  │
                         │ sync state          │
                         └──────┬────────┬─────┘
                                │        │
                   product text │        │ chunks + embeddings
                                ▼        ▼
                     ┌──────────────┐  ┌────────────────┐
                     │ Elasticsearch│  │ pgvector       │
                     │ lexical index│  │ vector index   │
                     └──────┬───────┘  └───────┬────────┘
                            │                  │
                            └────────┬─────────┘
                                     ▼
                           ┌───────────────────┐
                           │ Hybrid retrieval  │
                           │ RRF + ranking     │
                           └─────────┬─────────┘
                                     ▼
                           ┌───────────────────┐
                           │ PostgreSQL cards  │
                           │ + retrieved chunks│
                           └─────────┬─────────┘
                                     ▼
                           ┌───────────────────┐
                           │ Prompt + guardrails│
                           └─────────┬─────────┘
                                     ▼
                           ┌───────────────────┐
                           │ Ollama or DeepSeek│
                           └─────────┬─────────┘
                                     ▼
                                API response
```

### Main components

  -----------------------------------------------------------------------
  Component                           Responsibility
  ----------------------------------- -----------------------------------
  `RP.Rag.Domain`                     Product catalog entities, product
                                      documents, chunks, and
                                      synchronization state.

  `RP.Rag.Application`                Search and embedding abstractions,
                                      chunk construction, ranking, prompt
                                      building, and request/response
                                      models.

  `RP.Rag.Infrastructure`             EF Core/PostgreSQL persistence,
                                      pgvector queries, Elasticsearch
                                      integration, embedding/generation
                                      clients, and seed loading.

  `RP.Rag.Indexer`                    Background workers that load seed
                                      data, synchronize product content
                                      to Elasticsearch, and create/update
                                      embeddings.

  `RP.Rag.Api`                        HTTP endpoints for lexical,
                                      semantic, hybrid search, and
                                      RAG-based question answering.
  -----------------------------------------------------------------------

## Retrieval flow

### 1. Lexical search

Elasticsearch indexes searchable product fields such as names,
summaries, descriptions, category names, tags, and combined search text.
Field boosts and phrase/partial matching help prioritize stronger
textual matches.

For Persian text, the index defines a `persian_custom` analyzer on `.fa`
multi-fields. Its analysis includes zero-width non-joiner handling,
digit normalization, Arabic/Persian character normalization, Persian
stop words, and conservative plural-suffix removal. The lexical query
can search both the default fields and their Persian sub-fields.

### 2. Semantic search

Product content is split into chunks. The chunk builder creates a
summary chunk and chunks for descriptions and active product documents.
Longer sections are divided into smaller pieces, and the product
name/category are prefixed to section text used for embedding.

The embedding provider is currently implemented through Ollama. The
default model is `bge-m3`, which produces **1,024-dimensional vectors**
in this configuration. Vectors are stored in PostgreSQL using pgvector.
`ProductChunks.Embedding` has an HNSW index configured for cosine
distance.

Changing the embedding model is not just a configuration switch: its
output dimensions must match the database vector column, and existing
chunks need to be re-embedded.

### 3. Hybrid search

Hybrid retrieval runs lexical and semantic search and combines their
ranks with **Reciprocal Rank Fusion (RRF)**, using `k = 60` by default.
A chunk can receive a contribution from its own semantic rank and from
the lexical rank of its product. Products found only by lexical search
can still contribute a summary or description chunk.

The implementation also contains a category-token index intended to help
infer a likely category from query terms. As with any ranking heuristic,
its effect should be validated against representative queries rather
than assumed to improve relevance in every catalog.

### 4. RAG / Ask

The `/api/ask` flow:

1.  Retrieves relevant products and chunks through hybrid retrieval.
2.  Loads product cards from PostgreSQL.
3.  Builds a prompt containing catalog facts and retrieved excerpts.
4.  Sends the prompt to the configured answer-generation provider.
5.  Returns the answer, model name, elapsed time, product cards, and
    source metadata.

Answers use references such as `[1]` to refer to the product cards in
the response. If retrieval returns no products, the service returns a
configured no-results response instead of calling the generation model.

The system prompt tells the assistant to use the supplied catalog
context for product-specific facts, avoid guessing missing product
details, treat retrieved text as data rather than instructions, and stay
within the product-catalog scope. URLs are stripped from generated
output as an additional check.

**Important:** prompts and output filtering are not a hard security
boundary. Do not treat these controls as a substitute for authorization,
input/output validation, monitoring, or adversarial testing.

## Technology stack

-   .NET 10 / ASP.NET Core
-   Entity Framework Core
-   PostgreSQL
-   [pgvector](https://github.com/pgvector/pgvector) for vector storage
    and similarity search
-   [Elasticsearch](https://www.elastic.co/elasticsearch) for lexical
    retrieval
-   [Ollama](https://ollama.com) for local embeddings and, optionally,
    local answer generation
-   DeepSeek API as an alternative answer-generation provider
-   Scalar for the API reference UI

## Prerequisites

Install or prepare:

-   .NET 10 SDK
-   Docker with Compose
-   Ollama running on the host, unless you configure the
    generation/embedding services differently
-   The required Ollama models:

``` bash
ollama pull bge-m3
ollama pull gemma4:e4b
```

The current embedding implementation expects 1,024 dimensions. The
generation model can be changed through configuration to another
compatible Ollama chat model.

## Run locally

### 1. Start infrastructure

From the repository root:

``` bash
docker compose up -d
```

This should start the PostgreSQL/pgvector and Elasticsearch services
defined by the repository's Compose configuration. Check the Compose
file and local port mappings if your environment differs.

### 2. Configure the services

Review the configuration in:

-   `src/RP.Rag.Api/appsettings.json`
-   `src/RP.Rag.Indexer/appsettings.json`

The default local endpoints are:

-   PostgreSQL: `localhost:5432`
-   Elasticsearch: `http://localhost:9200`
-   Ollama: `http://localhost:11434`

Do not commit real credentials or API keys. For DeepSeek, configure the
API key using a development secret or environment variable rather than
committing it to source control. If the API runs inside Docker,
`localhost` refers to the container itself; use the appropriate service
name or host address for each dependency.

### 3. Run the indexer

``` bash
dotnet run --project src/RP.Rag.Indexer
```

The indexer seeds the database and runs background workers for
Elasticsearch indexing and embedding. Let the initial processing
complete before evaluating semantic or hybrid search. The workers poll
for pending changes and retry processing failures.

### 4. Run the API

In another terminal:

``` bash
dotnet run --project src/RP.Rag.Api
```

In Development, the API is configured to apply database migrations at
startup.

The default HTTP URL is:

``` text
http://localhost:5290
```

The Scalar API reference is available at:

``` text
http://localhost:5290/scalar/v1
```

Ready-made HTTP requests are included in
`src/RP.Rag.Api/RP.Rag.Api.http`.

## API endpoints

  -----------------------------------------------------------------------------------------------
  Method                  Endpoint                                        Purpose
  ----------------------- ----------------------------------------------- -----------------------
  `GET`                   `/api/search/lexical?q={query}&topK={count}`    Search Elasticsearch
                                                                          lexical matches.

  `GET`                   `/api/search/semantic?q={query}&topK={count}`   Search using vector
                                                                          similarity.

  `GET`                   `/api/search/hybrid?q={query}&topK={count}`     Combine lexical and
                                                                          semantic retrieval.

  `POST`                  `/api/ask`                                      Retrieve product
                                                                          context and generate a
                                                                          grounded answer.
  -----------------------------------------------------------------------------------------------

The search endpoints require a non-empty `q` query parameter. `topK` is
optional; the API defaults it to `10` for search requests. The Ask
endpoint accepts a JSON body with `question` and optional `topK`.

### Example: hybrid search

``` bash
curl --get "http://localhost:5290/api/search/hybrid" \
  --data-urlencode "q=best motion sensor for cable-free use" \
  --data-urlencode "topK=10"
```

### Example: ask a product question

``` bash
curl -X POST "http://localhost:5290/api/ask" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is the return policy for this speaker?",
    "topK": 3
  }'
```

The response includes the original question, answer, generation model,
elapsed time, product cards, and source metadata. Exact answer content
and timing depend on the indexed data and the configured model.

## Configuration reference

The principal settings are in the API and Indexer `appsettings.json`
files.

  -------------------------------------------------------------------------------------------------
  Setting                                      Default / example            Notes
  -------------------------------------------- ---------------------------- -----------------------
  `ConnectionStrings:Postgres`                 Local PostgreSQL connection  Database connection
                                                                            used by the
                                                                            application.

  `Elasticsearch:Url`                          `http://localhost:9200`      Elasticsearch endpoint.

  `Elasticsearch:ProductIndexName`             `products`                   Product index name.

  `Elasticsearch:RequestTimeoutSeconds`        `30`                         Elasticsearch request
                                                                            timeout.

  `Ollama:Url`                                 `http://localhost:11434`     Ollama endpoint.

  `Ollama:EmbeddingModel`                      `bge-m3:latest`              Embedding model;
                                                                            currently expected to
                                                                            return 1,024
                                                                            dimensions.

  `Ollama:GenerationModel`                     `gemma4:e4b`                 Default local
                                                                            answer-generation
                                                                            model.

  `Ollama:Temperature`                         `0.2`                        Generation temperature.

  `Generation:Provider`                        `Ollama`                     Select `Ollama` or
                                                                            `DeepSeek` for answer
                                                                            generation.

  `DeepSeek:Url`                               `https://api.deepseek.com`   DeepSeek API base URL.

  `DeepSeek:ApiKey`                            Empty in the sample          Supply securely when
                                                                            using DeepSeek.

  `DeepSeek:GenerationModel`                   `deepseek-flash`             Configured generation
                                                                            model identifier.

  `Ask:TopProducts`                            `5`                          Number of products to
                                                                            use as answer context
                                                                            by default.

  `Ask:ChunksPerProduct`                       `2`                          Maximum retrieved
                                                                            excerpts per product
                                                                            for Ask.

  `Ask:MaxQuestionLength`                      `500`                        Questions are trimmed
                                                                            to this maximum length.

  `EmbeddingWorker:PollIntervalSeconds`        `5`                          Embedding worker
                                                                            polling interval.

  `EmbeddingWorker:BatchSize`                  `5`                          Number of pending
                                                                            product sync rows
                                                                            processed per
                                                                            iteration.

  `EmbeddingWorker:MaxChunksPerBatch`          `2000`                       Upper bound for a batch
                                                                            of chunk texts sent for
                                                                            embedding.

  `ProductIndexerWorker:PollIntervalSeconds`   `30`                         Elasticsearch indexer
                                                                            polling interval.
  -------------------------------------------------------------------------------------------------

Use environment-specific configuration for credentials, ports, timeouts,
and model choices.

## Persian lexical search

The Elasticsearch index defines Persian multi-fields for product names,
summaries, descriptions, category names, and combined search text. The
`persian_custom` analyzer includes:

-   Handling of the zero-width non-joiner.
-   Lowercasing and normalization of Persian/Arabic character variants.
-   Conversion of Persian digits to ASCII digits.
-   Persian stop-word filtering.
-   Conservative removal of `ها` and `های` plural suffixes when enough
    stem characters remain.

The lexical query searches both the default text fields and Persian
`.fa` sub-fields, allowing English and Persian text to coexist in the
index. This is **lexical normalization**, not a claim that Persian
semantic embeddings are accurate; evaluate each retrieval strategy
separately on Persian-language queries.

When the Elasticsearch mapping changes, the index must be rebuilt and
the corresponding products must be marked for synchronization. The
existing indexer uses product synchronization state to skip unchanged
records. For local development, follow the reset/reindex process below
only after confirming that deleting the index is safe for your
environment.

Example analyzer inspection:

``` bash
curl -X POST "http://localhost:9200/products/_analyze" \
  -H "Content-Type: application/json" \
  -d '{
    "field": "name.fa",
    "text": "نمونه عبارت فارسی"
  }'
```

To force a full rebuild in a disposable local environment, the existing
README workflow is:

``` bash
curl -X DELETE "http://localhost:9200/products"
```

Then reset the relevant `ProductSyncs` rows so the indexer processes the
product content again, restart the API so the index is recreated, and
restart the indexer. **Deleting the index removes its indexed documents;
do not run this against an environment whose index you need to
preserve.**

## Seed data and query examples

The repository contains JSON seed-data loaders for categories, tags,
products, and product documents. Product documents can represent content
such as warranties, return policies, manuals, specifications, and FAQs.
The indexer uses synchronization records to track pending indexing and
embedding work.

`example_query.md` provides examples for the four API paths:

-   **Lexical:** direct keywords, tags, and product names.
-   **Semantic:** intent-oriented phrasing that may not repeat the
    catalog's exact tags.
-   **Hybrid:** a concrete product term plus a semantic constraint.
-   **Ask/RAG:** document lookup or synthesis across multiple products.

The query examples include expected product IDs based on the labeled
query data. Treat these as evaluation targets, not as a guarantee that
every model or configuration will return identical rankings.

## Evaluating retrieval

A useful evaluation should separate retrieval quality from answer
quality.

1.  Run a stable set of representative lexical, semantic, and hybrid
    queries.
2.  Compare returned product IDs with the labeled relevant product IDs
    in the seed query data.
3.  Track ranking metrics such as Precision@K, Recall@K, and Mean
    Reciprocal Rank (MRR), if implemented in your evaluation tooling.
4.  Test document-grounded questions whose answers exist in
    warranty/manual/return documents.
5.  Test missing-information questions and verify the assistant says
    when the catalog context does not contain an answer.
6.  Repeat tests after changing analyzers, embedding models, chunking,
    ranking heuristics, or prompt rules.

A relevant product appearing somewhere in the results is not enough by
itself: the ranking, source chunk, and final answer all matter.

## Change tracking and reprocessing

`ProductSyncInterceptor` detects changes to relevant product data, tags,
and documents, then marks products for re-indexing and re-embedding. The
workers use the synchronization state to process pending work rather
than rebuilding every product on every poll.

If you change the Elasticsearch mapping or the embedding model, ordinary
change tracking may not be sufficient on its own. Follow a deliberate
reindex/re-embedding procedure and verify that the model name, vector
dimensions, and stored embeddings remain consistent.

## EF Core migrations

The project uses EF Core migrations from the Infrastructure project.
Typical commands are:

``` bash
dotnet ef migrations add <MigrationName> \
  --project src/RP.Rag.Infrastructure \
  --startup-project src/RP.Rag.Api \
  --output-dir Data/Migrations
```

``` bash
dotnet ef database update \
  --project src/RP.Rag.Infrastructure \
  --startup-project src/RP.Rag.Api
```

Review generated migrations before applying them to any database
containing data you need to preserve.

## Project layout

``` text
src/
├── RP.Rag.Domain/
│   └── Entities/
├── RP.Rag.Application/
│   ├── Ask/
│   ├── Catalog/
│   ├── Embeddings/
│   ├── Prompts/
│   └── Search/
├── RP.Rag.Infrastructure/
│   ├── Ask/
│   ├── Catalog/
│   ├── Data/
│   ├── Elastic/
│   ├── Embeddings/
│   ├── Search/
│   └── SeedData/
├── RP.Rag.Indexer/
│   └── Workers/
└── RP.Rag.Api/
    └── Controllers/

data/
└── seed/

example_query.md
```

## Current limitations and production considerations

-   **Retrieval is not automatically accurate.** Search quality depends
    on catalog quality, analyzer configuration, embeddings, chunking,
    ranking, and evaluation.
-   **The embedding schema is dimension-specific.** The current vector
    column is `vector(1024)`.
-   **Prompt instructions are not a security boundary.** Test indirect
    prompt injection in product documents and user queries; add
    appropriate controls for any public deployment.
-   **Generated answers can still be wrong.** Grounding and citations
    reduce risk but do not guarantee factual correctness. Validate
    critical fields against the database.
-   **Provider configuration matters.** Ollama supports local inference;
    DeepSeek answer generation sends prompts to an external service and
    has different privacy, availability, and cost implications.
-   **Credentials must be protected.** Do not commit API keys or
    production passwords.
-   **Operational hardening is still required** before public
    deployment, including authentication/authorization where needed,
    rate limiting, monitoring, error handling, backups, and resource
    limits.

## License

See [`LICENSE`](LICENSE).
