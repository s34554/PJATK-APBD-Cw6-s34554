using System.Data;
using Microsoft.Data.SqlClient;
using WebApplication1.DTOs;
using WebApplication1.Exceptions;

namespace WebApplication1.Services;

public class AppointmentService(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
                                                ?? throw new InvalidOperationException("Missing DefaultConnection.");

    public async Task<List<AppointmentListDto>> GetAllAsync(string? status, string? patientLastName)
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
                           WHERE (@Status IS NULL OR a.Status = @Status)
                           AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                           ORDER BY a.AppointmentDate;
                           """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = (object?)status ?? DBNull.Value;
        cmd.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 100).Value = (object?)patientLastName ?? DBNull.Value;
        await using var reader = await cmd.ExecuteReaderAsync();

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
        
        await EnsurePatientActiveAsync(connection, request.IdPatient);
        await EnsureDoctorActiveAsync(connection, request.IdDoctor);
        await EnsureNoConflictAsync(connection, request.IdDoctor, request.AppointmentDate, excludeId: null);
        
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
    public async Task UpdateAsync(int idAppointment, UpdateAppointmentRequestDto request)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var (currentDate, currentStatus) = await GetCurrentStateAsync(connection, idAppointment);
        
        await EnsurePatientActiveAsync(connection, request.IdPatient);
        await EnsureDoctorActiveAsync(connection, request.IdDoctor);
        
        if (currentStatus == "Completed" && currentDate != request.AppointmentDate)
            throw new ConflictException("Cannot change date of a completed appointment.");
        
        if (currentDate != request.AppointmentDate)
            await EnsureNoConflictAsync(connection, request.IdDoctor, request.AppointmentDate, excludeId: idAppointment);
        
        const string updateSql = """
                                 UPDATE dbo.Appointments
                                 SET IdPatient = @IdPatient,
                                     IdDoctor = @IdDoctor,
                                     AppointmentDate = @AppointmentDate,
                                     Status = @Status,
                                     Reason = @Reason,
                                     InternalNotes = @InternalNotes
                                 WHERE IdAppointment = @IdAppointment;
                                 """;
        await using (var cmd = new SqlCommand(updateSql, connection))
        {
            cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
            cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
            cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
            cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = request.Status;
            cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;
            cmd.Parameters.Add("@InternalNotes", SqlDbType.NVarChar, 500).Value 
                = (object?)request.InternalNotes ?? DBNull.Value;
            cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
            await cmd.ExecuteNonQueryAsync();
        }
    }
    private async Task EnsurePatientActiveAsync(SqlConnection connection, int idPatient)
    {
        const string sql = "SELECT IsActive FROM dbo.Patients WHERE IdPatient = @IdPatient;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = idPatient;
        var result = await cmd.ExecuteScalarAsync();
        if (result is null) throw new BusinessException("Patient does not exist.");
        if (!(bool)result) throw new BusinessException("Patient is not active.");
    }

    private async Task EnsureDoctorActiveAsync(SqlConnection connection, int idDoctor)
    {
        const string sql = "SELECT IsActive FROM dbo.Doctors WHERE IdDoctor = @IdDoctor;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
        var result = await cmd.ExecuteScalarAsync();
        if (result is null) throw new BusinessException("Doctor does not exist.");
        if (!(bool)result) throw new BusinessException("Doctor is not active.");
    }
    private async Task EnsureNoConflictAsync(
        SqlConnection connection, 
        int idDoctor, 
        DateTime appointmentDate, 
        int? excludeId)
    {
        const string sql = """
                           SELECT COUNT(*) FROM dbo.Appointments
                           WHERE IdDoctor = @IdDoctor 
                             AND AppointmentDate = @AppointmentDate
                             AND Status = N'Scheduled'
                             AND (@ExcludeId IS NULL OR IdAppointment <> @ExcludeId);
                           """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
        cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointmentDate;
        cmd.Parameters.Add("@ExcludeId", SqlDbType.Int).Value 
            = (object?)excludeId ?? DBNull.Value;

        var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        if (count > 0) 
            throw new ConflictException("Doctor already has an appointment at this time.");
    }
    private async Task<(DateTime Date, string Status)> GetCurrentStateAsync(
        SqlConnection connection, 
        int idAppointment)
    {
        const string sql = """
                           SELECT AppointmentDate, Status FROM dbo.Appointments
                           WHERE IdAppointment = @IdAppointment;
                           """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
    
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new NotFoundException($"Appointment {idAppointment} not found.");
    
        return (reader.GetDateTime(0), reader.GetString(1));
    }

    public async Task DeleteAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
    
        var (_, currentStatus) = await GetCurrentStateAsync(connection, idAppointment);
    
        if (currentStatus == "Completed")
            throw new ConflictException("Cannot delete a completed appointment.");
    
        const string sql = "DELETE FROM dbo.Appointments WHERE IdAppointment = @IdAppointment;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        await cmd.ExecuteNonQueryAsync();
    }
}