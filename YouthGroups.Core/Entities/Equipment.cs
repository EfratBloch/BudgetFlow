using YouthGroups.Core.Enums;

namespace YouthGroups.Core.Entities;

// One physical item in the store ("Projector 1", "Projector 2" are two rows).
// This is the limited resource that guides compete for.
public class Equipment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EquipmentStatus Status { get; set; }

    // Both null while the item is available
    public int? HolderUserId { get; set; }
    public User? Holder { get; set; }
    public DateTime? TakenAt { get; set; } // UTC

    // Concurrency token. Mapped in the Data layer to PostgreSQL's hidden system
    // column xmin, which the database changes on every write to the row.
    // The application never sets it.
    public uint Version { get; set; }
}
