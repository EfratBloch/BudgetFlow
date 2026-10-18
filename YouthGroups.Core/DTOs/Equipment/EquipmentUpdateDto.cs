using System.ComponentModel.DataAnnotations;

namespace YouthGroups.Core.DTOs;

// Request: the coordinator edits details or sends an item to / back from maintenance.
// Taking and returning an item go through /api/equipment/{id}/holder, never through here.
public class EquipmentUpdateDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    // true = Maintenance, false = Available. Not allowed while the item is taken.
    public bool InMaintenance { get; set; }
}
