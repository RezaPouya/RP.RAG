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
        entity.Property(x => x.UserId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.DistilledMemory).HasColumnType("text");
        entity.Property(x => x.SemanticMemoryJson).HasColumnType("jsonb");
        entity.Property(x => x.EpisodicMemoryJson).HasColumnType("jsonb");
        entity.Property(x => x.MessageCount).IsRequired();
        entity.Property(x => x.DistilledThroughSequence).IsRequired();
        entity.HasIndex(x => x.UserId); // برای سرعت بیشتر در لیست کردن گفتگوهای کاربر
    }
}
