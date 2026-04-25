using System.Data;
using Microsoft.Data.SqlClient;
using WebApplication1.DTOs;

namespace WebApplication1.Services;

public class AppointmentService(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
                                                ?? throw new InvalidOperationException("Missing DefaultConnection.");

    public async Task<List<AppointmentListDto>> GetAllAsync()
    {
        var result = new List<AppointmentListDto>();

        const string sql = """
                           SELECT
                               a.IdAppointment,
                               a.AppointmentDate,
                               a.Status,
                               a.Reason,
                               p.FirstName + N' ' + p.LastName AS PatientFullName,
                               p.Email AS PatientEmail
                           FROM dbo.Appointments a
                           JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                           ORDER BY a.AppointmentDate;
                           """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            });
        }
        return result;
    }

    public async Task<AppointmentDetailsDto?> GetByIdAsync(int id)
    {
        const string sql = """
                           SELECT
                               a.IdAppointment,
                               a.AppointmentDate,
                               a.Status,
                               a.Reason,
                               a.InternalNotes,
                               a.CreatedAt,
                               p.FirstName + N' ' + p.LastName AS PatientFullName,
                               p.Email AS PatientEmail,
                               p.PhoneNumber AS PatientPhoneNumber,
                               d.FirstName + N' ' + d.LastName AS DoctorFullName,
                               d.LicenseNumber AS DoctorLicenseNumber,
                               s.Name AS SpecializationName
                           FROM dbo.Appointments a
                           JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                           JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                           JOIN dbo.Specializations s ON s.IdSpecialization = d.IdSpecialization
                           WHERE a.IdAppointment = @id
                           """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync()) return null;

        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(0),
            AppointmentDate = reader.GetDateTime(1),
            Status = reader.GetString(2),
            Reason = reader.GetString(3),
            InternalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = reader.GetDateTime(5),
            PatientFullName = reader.GetString(6),
            PatientEmail = reader.GetString(7),
            PatientPhoneNumber = reader.GetString(8),
            DoctorFullName = reader.GetString(9),
            DoctorLicenseNumber = reader.GetString(10),
            SpecializationName = reader.GetString(11)
        };
    }
}