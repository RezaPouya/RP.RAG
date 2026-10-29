using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities;

namespace RP.Rag.Infrastructure.Data.Configurations;

internal sealed class ConversationDbConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> entity)
    {
        entity.ToTable("Conversations");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.DistilledMemory).HasColumnType("text");
        entity.Property(x => x.SemanticMemoryJson).HasColumnType("jsonb");
        entity.Property(x => x.EpisodicMemoryJson).HasColumnType("jsonb");
        entity.Property(x => x.MessageCount).IsRequired();
        entity.Property(x => x.DistilledThroughSequence).IsRequired();
    }
}

internal sealed class ConversationMessageDbConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> entity)
    {
        entity.ToTable("ConversationMessages");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Content).HasColumnType("text").IsRequired();
        entity.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
        entity.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
