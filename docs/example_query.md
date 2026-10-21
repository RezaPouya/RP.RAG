# Test Queries for Each Endpoint

These are drawn from your existing `queries.json` where possible (marked with query ID) and supplemented with additional realistic examples.

---

## 1. Lexical Search Endpoint

Lexical search relies on keyword matching (exact terms, tags, names).

| # | Query | Source | Expected Top Results |
|---|-------|--------|----------------------|
| 1 | `budget motion sensor` | Query ID 1 | Product IDs 29, 187, 262, 487, 670, 775 |
| 2 | `rechargeable desktop speakers` | Query ID 5 | Product IDs 84, 143, 165, 194, 480 |
| 3 | `wireless Bluetooth speaker` | Query ID 16 | Product IDs 161, 196, 252, 295, 569, 632, 669, 753, 780, 894 |
| 4 | `premium GaN wall charger` | Query ID 27 | Product IDs 26, 55, 519, 626, 736, 858 |
| 5 | `budget Ethernet cable` | Query ID 35 | Product IDs 409, 479, 652, 675, 696, 996 |

**Why these work:** They match directly on tag names (budget, rechargeable, wireless, bluetooth, premium), product names (GaN wall charger, Ethernet cable), and category terms.

---

## 2. Semantic Search Endpoint

Semantic search relies on meaning/intent, not exact keyword overlap.

| # | Query | Source | Expected Top Results |
|---|-------|--------|----------------------|
| 1 | `I need a tablet that charges very quickly` | Query ID 2 | Product IDs 8, 112, 156, 326, 344, 443, 493, 540, 576, 584 |
| 2 | `I need a smartphone with a high-end feel` | Query ID 3 | Product IDs 83, 94, 119, 159, 167, 215, 299, 303, 393, 396 |
| 3 | `I need a laptop with colorful lighting` | Query ID 22 | Product IDs 4, 146, 244, 324, 342, 351, 400, 406, 474, 646 |
| 4 | `I need a smartwatch that charges very quickly` | Query ID 23 | Product IDs 17, 169, 195, 236, 274, 289, 411, 454, 471, 592 |
| 5 | `I need a charger that takes up very little space` | Query ID 107 | Product IDs 13, 18, 55, 152, 178, 224, 250, 300, 370, 375 |

**Why these work:** They describe intent ("charges very quickly" → fast-charging tag, "high-end feel" → premium tag, "colorful lighting" → rgb tag, "takes up very little space" → compact tag) without using the exact tag words.

---

## 3. Hybrid Search Endpoint

Hybrid search combines lexical + semantic signals.

| # | Query | Source | Expected Top Results |
|---|-------|--------|----------------------|
| 1 | `best travel router for a premium setup` | Query ID 4 | Product IDs 545, 552 |
| 2 | `best motion sensor for cable-free use` | Query ID 6 | Product IDs 29, 262, 548, 854 |
| 3 | `best desktop speakers for a colorful setup` | Query ID 9 | Product IDs 165, 636, 785, 905 |
| 4 | `best gaming laptop for USB-C devices` | Query ID 15 | Product IDs 101, 400, 575, 643, 685, 903 |
| 5 | `best ergonomic office chair for daily use without replacing batteries` | Query ID 17 | Product IDs 574, 654, 809 |

**Why these work:** They contain both a concrete keyword anchor ("travel router", "motion sensor", "desktop speakers", "gaming laptop", "ergonomic office chair") and a semantic constraint ("premium setup", "cable-free use", "colorful setup", "USB-C devices", "without replacing batteries").

---

## 4. RAG / Ask Endpoint

RAG (Retrieval-Augmented Generation) answers natural-language questions using retrieved product data.

| # | Query | Source | What RAG Should Answer |
|---|-------|--------|------------------------|
| 1 | `What is the return policy for the Zenith Prime Mini Shower Speaker?` | Document-based | "30 days from delivery, full refund, original packaging required, free return shipping in continental US." (from product 1's return doc) |
| 2 | `How do I pair the Kestrel Neo SE keyboard?` | Document-based | "Plug in USB receiver or select Bluetooth mode with Fn+1, hold Fn+1 for 3 seconds, choose 'Kestrel Neo SE' on your computer, use Fn+Esc to reset." (from product 2's manual doc) |
| 3 | `What warranty covers the Polaris Edge Pro and for how long?` | Document-based | "12-month limited warranty against manufacturing defects. Does not cover physical damage, liquid damage, or normal wear and tear. SKU ST-POLED-0687." (from product 3's warranty doc) |
| 4 | `Which budget motion sensors do you have and what features do they offer?` | Retrieval + synthesis | Should retrieve products 29, 187, 262, 487, 670, 775 and summarize their features (Wi-Fi, compact, voice assistant, etc.) |
| 5 | `I need a tablet that charges very quickly — what do you recommend?` | Retrieval + synthesis | Should retrieve products 8, 112, 156, 326, 344, 443, 493, 540, 576, 584 and recommend based on fast-charging tag |

**Why these work:** Queries 1–3 require pulling from `documents[]` (warranty, return, manual) attached to products. Queries 4–5 require semantic retrieval + generative summarization across multiple products.

---

## Quick Reference: Endpoint → Query Type Mapping

| Endpoint | Query Style | Example |
|----------|-------------|---------|
| Lexical | Keywords, tags, exact names | `budget motion sensor` |
| Semantic | Intent, natural language | `I need a tablet that charges very quickly` |
| Hybrid | Keyword + intent combo | `best travel router for a premium setup` |
| RAG/Ask | Questions needing document lookup or multi-product synthesis | `What is the return policy for the Zenith Prime Mini Shower Speaker?` |

All 5 lexical, 5 semantic, and 5 hybrid queries above are directly from your `queries.json` with known `relevantProductIds`, so you can measure precision/recall. The RAG queries are designed to exercise the `documents` array and multi-product retrieval.