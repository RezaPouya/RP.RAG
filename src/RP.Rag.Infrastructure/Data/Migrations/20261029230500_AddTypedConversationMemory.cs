using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RP.Rag.Infrastructure.Data.Migrations;

public sealed partial class AddTypedConversationMemory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SemanticMemoryJson",
            table: "Conversations",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EpisodicMemoryJson",
            table: "Conversations",
            type: "jsonb",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SemanticMemoryJson", table: "Conversations");
        migrationBuilder.DropColumn(name: "EpisodicMemoryJson", table: "Conversations");
    }
}
