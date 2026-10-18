using YouthGroups.Core.Enums;

namespace YouthGroups.Core.DTOs;

// Response
public class EquipmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EquipmentStatus Status { get; set; }
    public int? HolderUserId { get; set; }
    public string? HolderName { get; set; }
    public DateTime? TakenAt { get; set; }
}
