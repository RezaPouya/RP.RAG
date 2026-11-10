using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RP.Rag.Domain.Entities;

internal sealed class ConversationMessageDbConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> entity)
    {
        entity.ToTable("ConversationMessages");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Content).HasColumnType("text").IsRequired();
        entity.Property(x => x.ProductIdsJson).HasColumnType("jsonb"); // ✅ NEW
        entity.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
        entity.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}