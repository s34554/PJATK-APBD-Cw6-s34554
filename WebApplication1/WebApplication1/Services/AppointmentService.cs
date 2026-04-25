using System.Data;
using Microsoft.Data.SqlClient;
using WebApplication1.DTOs;
using WebApplication1.Exceptions;

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

    public async Task<int?> CreateAsync(CreateAppointmentRequestDto request)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        const string patientSql = """
                                  SELECT IsActive FROM dbo.Patients WHERE IdPatient = @IdPatient;
                                  """;
        await using (var cmd = new SqlCommand(patientSql, connection))
        {
            cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
            var result = await cmd.ExecuteScalarAsync();
            if (result is null) throw new BusinessException("Patient does not exist.");
            if ((bool)result == false) throw new BusinessException("Patient is not active.");
        }
        
        const string doctorSql = """
                                 SELECT IsActive FROM dbo.Doctors WHERE IdDoctor = @IdDoctor;
                                 """;
        await using (var cmd = new SqlCommand(doctorSql, connection))
        {
            cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
            var result = await cmd.ExecuteScalarAsync();
            if (result is null) throw new BusinessException("Doctor does not exist.");
            if ((bool)result == false) throw new BusinessException("Doctor is not active.");
        }
        
        const string conflictSql = """
                                   SELECT COUNT(*) FROM dbo.Appointments
                                   WHERE IdDoctor = @IdDoctor 
                                     AND AppointmentDate = @AppointmentDate
                                     AND Status = N'Scheduled';
                                   """;
        await using (var cmd = new SqlCommand(conflictSql, connection))
        {
            cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
            cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            if (count > 0) throw new ConflictException("Doctor already has an appointment at this time.");
        }
        
        const string insertSql = """
                                 INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                                 OUTPUT INSERTED.IdAppointment
                                 VALUES (@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason);
                                 """;
        await using (var cmd = new SqlCommand(insertSql, connection))
        {
            cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
            cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
            cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
            cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;
            var newId = (int)(await cmd.ExecuteScalarAsync())!;
            return newId;
        }
    }
}