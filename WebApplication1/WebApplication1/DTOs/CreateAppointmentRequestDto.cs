using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public class CreateAppointmentRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "IdPatient must be a positive integer.")]
    public int IdPatient { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "IdDoctor must be a positive integer.")]
    public int IdDoctor { get; set; }
    [Required]
    public DateTime AppointmentDate { get; set; }
    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(250, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 250 characters.")]
    public string Reason { get; set; } = string.Empty;
}