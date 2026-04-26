using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public class UpdateAppointmentRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "IdPatient must be a positive integer.")]
    public int IdPatient { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "IdDoctor must be a positive integer.")]
    public int IdDoctor { get; set; }
    [Required]
    public DateTime AppointmentDate { get; set; }
    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Scheduled|Completed|Cancelled)$", 
        ErrorMessage = "Status must be Scheduled, Completed or Cancelled.")]
    public string Status { get; set; } = string.Empty;
    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(250, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 250 characters.")]
    public string Reason { get; set; } = string.Empty;
    [StringLength(500, ErrorMessage = "InternalNotes must be at most 500 characters.")]
    public string? InternalNotes { get; set; }
}