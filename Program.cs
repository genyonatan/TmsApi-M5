using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

builder.Services.AddAuthorization();


builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();


builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();
builder.Services.AddDbContext<TmsDbContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("TmsDatabase")
        )
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging()
);

var app = builder.Build();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
}))
.RequireAuthorization();

app.MapGet("/api/enrollments/worker-smoke", (EnrollmentWorker worker) =>
{
    worker.ProcessBatch();
    return Results.Ok("processed");
});

app.Map("/error", () => Results.Problem("An unexpected error occurred."));

app.MapPost("/api/enrollments/test", async (
    IEnrollmentService enrollmentService,
    string studentId,
    string courseCode) =>
{
    var record = await enrollmentService.EnrollAsync(studentId, courseCode);
    return Results.Ok(record);
});

app.MapGet("/api/enrollments/test/{id}", async (
    IEnrollmentService enrollmentService,
    string id) =>
{
    var record = await enrollmentService.GetByIdAsync(id);

    return record is null
        ? Results.NotFound()
        : Results.Ok(record);
});

app.MapDelete("/api/enrollments/test/{id}", async (
    IEnrollmentService enrollmentService,
    string id) =>
{
    var removed = await enrollmentService.DeleteAsync(id);

    return removed
        ? Results.Ok("deleted")
        : Results.NotFound();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException(
        "Simulated database failure for ProblemDetails testing"
    );
});

// Seed test data at startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<TmsDbContext>();

    // Apply any migrations that have not been applied yet
    context.Database.Migrate();

    // Only seed when the Students table is empty
    if (!context.Students.Any())
    {
        var students = new List<Student>
        {
            new()
            {
                RegistrationNumber = "TMS-2026-0001",
                Name = "Alice Smith",
                GPA = 3.8m,
                IsActive = true
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0002",
                Name = "Bob Jones",
                GPA = 2.9m,
                IsActive = true
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0003",
                Name = "Charlie Brown",
                GPA = 3.4m,
                IsActive = false
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0004",
                Name = "Diana Prince",
                GPA = 3.9m,
                IsActive = true
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0005",
                Name = "Evan Wright",
                GPA = 2.5m,
                IsActive = true
            }
        };

        context.Students.AddRange(students);

        var courses = new List<Course>
        {
            new()
            {
                Code = "CS-101",
                Title = "Introduction to Computer Science",
                Capacity = 30
            },
            new()
            {
                Code = "CS-201",
                Title = "Data Structures and Algorithms",
                Capacity = 25
            },
            new()
            {
                Code = "MAT-101",
                Title = "Calculus I",
                Capacity = 40
            }
        };

        context.Courses.AddRange(courses);

        // Save first so PostgreSQL generates Student and Course IDs
        context.SaveChanges();

        var enrollments = new List<Enrollment>
        {
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[0].Id,
                Grade = 4.0m
            },
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[1].Id,
                Grade = 3.6m
            },
            new()
            {
                StudentId = students[1].Id,
                CourseId = courses[0].Id,
                Grade = 2.8m
            },
            new()
            {
                StudentId = students[3].Id,
                CourseId = courses[1].Id,
                Grade = 3.9m
            }
        };

        context.Enrollments.AddRange(enrollments);
        context.SaveChanges();
    }
}

app.Run();


app.Run();