using Microsoft.AspNetCore.Mvc;
using WebApplication1.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(AppointmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AppointmentListDto>>> GetAll()
    {
        var appointments = await service.GetAllAsync();
        return Ok(appointments);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<ActionResult<AppointmentDetailsDto?>> GetById(int idAppointment)
    {
        var appointment = await service.GetByIdAsync(idAppointment);
        if (appointment is null) return NotFound();
        return Ok(appointment);
    }
}