using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RP.Rag.Infrastructure.Data.Migrations
{
    [DbContext(typeof(RpRagDbContext))]
    [Migration("20261012120000_AddChunkEmbeddingHnswIndex")]
    public partial class AddChunkEmbeddingHnswIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProductChunks_Embedding",
                table: "ProductChunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductChunks_Embedding",
                table: "ProductChunks");
        }
    }
}
