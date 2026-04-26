using Microsoft.AspNetCore.Mvc;
using WebApplication1.DTOs;
using WebApplication1.Exceptions;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(AppointmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AppointmentListDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
    {
        var appointments = await service.GetAllAsync(status, patientLastName);
        return Ok(appointments);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<ActionResult<AppointmentDetailsDto?>> GetById(int idAppointment)
    {
        var appointment = await service.GetByIdAsync(idAppointment);
        if (appointment is null) return NotFound();
        return Ok(appointment);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDetailsDto>> Create([FromBody] CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now)
            return BadRequest(new ErrorResponseDto { Message = "Appointment date cannot be in the past." });

        try
        {
            var newId = await service.CreateAsync(request);
            var details = await service.GetByIdAsync(newId!.Value);
            return CreatedAtAction(nameof(GetById), new { idAppointment = newId.Value }, details);
        }
        catch (BusinessException ex)
        {
            return BadRequest(new ErrorResponseDto { Message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new ErrorResponseDto { Message = ex.Message });
        }
    }
    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> Update(int idAppointment, [FromBody] UpdateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now)
            return BadRequest(new ErrorResponseDto { Message = "Appointment date cannot be in the past." });
        
        try
        {
            await service.UpdateAsync(idAppointment, request);
            var updated = await service.GetByIdAsync(idAppointment);
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponseDto { Message = ex.Message });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new ErrorResponseDto { Message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new ErrorResponseDto { Message = ex.Message });
        }
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> Delete(int idAppointment)
    {
        try
        {
            await service.DeleteAsync(idAppointment);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponseDto { Message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new ErrorResponseDto { Message = ex.Message });
        }
    }
}