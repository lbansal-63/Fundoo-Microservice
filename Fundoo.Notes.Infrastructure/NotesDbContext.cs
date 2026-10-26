using Fundoo.Notes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notes.Infrastructure.Persistence;

public class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<NoteLabel> NoteLabels => Set<NoteLabel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Note>(e =>
        {
            e.Property(n => n.Title).HasMaxLength(200);
            e.Property(n => n.Color).HasMaxLength(50);
            e.HasIndex(n => n.UserId);
        });

        b.Entity<Label>(e =>
        {
            e.Property(l => l.Name).HasMaxLength(100);
            e.HasIndex(l => new { l.UserId, l.Name }).IsUnique();
        });

        b.Entity<NoteLabel>(e =>
        {
            e.HasKey(x => new { x.NoteId, x.LabelId });
            e.HasOne(x => x.Note).WithMany(n => n.NoteLabels).HasForeignKey(x => x.NoteId);
            e.HasOne(x => x.Label).WithMany(l => l.NoteLabels).HasForeignKey(x => x.LabelId);
        });
    }
}