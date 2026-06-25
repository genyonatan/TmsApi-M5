
using Microsoft.AspNetCore.Mvc;
using TmsApi.Data;
using Microsoft.EntityFrameworkCore;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly TmsDbContext _context;

    public TestController(TmsDbContext context)
    {
        _context = context;
    }

    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine(
            "\n>>> STEP 1: Building the query object (no database contact)..."
        );

        var query = _context.Students
            .Where(student => student.GPA >= 3.0m);

        Console.WriteLine(
            ">>> STEP 2: Appending a sorting clause..."
        );

        var orderedQuery = query
            .OrderBy(student => student.Name);

        Console.WriteLine(
            ">>> STEP 3: Materializing query into a C# List..."
        );

        var results = orderedQuery.ToList();

        Console.WriteLine(
            ">>> STEP 4: Materialization finished. List populated.\n"
        );

        return Ok(results);
    }

    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine(
            "\n>>> STEP 1: Running non-translatable query..."
        );

        try
        {
            var students = _context.Students
                .Where(student => IsHonorRoll(student.GPA))
                .ToList();

            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $">>> EXCEPTION CAUGHT: {ex.Message}\n"
            );

            return BadRequest(new
            {
                Message = ex.Message
            });
        }
    }


    [HttpGet("active-high-gpa-count")]
    public async Task<IActionResult> GetActiveHighGpaCount()
    {
        var count = await _context.Students
            .Where(student =>
                student.IsActive &&
                student.GPA >= 3.0m)
            .CountAsync();

        return Ok(new
        {
            ActiveHighGpaStudentCount = count
        });
    }

    [HttpGet("courses-by-enrollment-count")]
    public async Task<IActionResult> GetCoursesByEnrollmentCount()
    {
        var courses = await _context.Courses
            .Select(course => new
            {
                course.Title,
                EnrollmentCount = course.Enrollments.Count
            })
            .OrderByDescending(course => course.EnrollmentCount)
            .ToListAsync();

        return Ok(courses);
    }

    [HttpGet("average-gpa-per-course")]
    public async Task<IActionResult> GetAverageGpaPerCourse()
    {
        var averages = await _context.Enrollments
            .GroupBy(enrollment => enrollment.Course.Title)
            .Select(group => new
            {
                Course = group.Key,
                AverageGPA = group.Average(
                    enrollment => enrollment.Student.GPA
                )
            })
            .ToListAsync();

        return Ok(averages);
    }

    [HttpGet("students-without-enrollments")]
    public async Task<IActionResult> GetStudentsWithoutEnrollments()
    {
        var students = await _context.Students
            .Where(student => !student.Enrollments.Any())
            .Select(student => student.Name)
            .ToListAsync();

        return Ok(students);
    }

    [HttpGet("students-without-enrollments-left-join")]
    public async Task<IActionResult> GetStudentsWithoutEnrollmentsLeftJoin()
    {
        var students = await _context.Students
            .LeftJoin(
                _context.Enrollments,
                student => student.Id,
                enrollment => enrollment.StudentId,
                (student, enrollment) => new
                {
                    Student = student,
                    Enrollment = enrollment
                }
            )
            .Where(result => result.Enrollment == null)
            .Select(result => result.Student.Name)
            .ToListAsync();

        return Ok(students);
    }

    [HttpGet("students-page")]
    public async Task<IActionResult> GetStudentsPage(
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 20;

        if (page < 1)
        {
            return BadRequest(new
            {
                Message = "Page number must be at least 1."
            });
        }

        var students = await _context.Students
            .OrderBy(student => student.Name)
            .ThenBy(student => student.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            Page = page,
            PageSize = pageSize,
            Students = students
        });
    }

    [HttpGet("top-five-courses")]
    public async Task<IActionResult> GetTopFiveCourses(
        CancellationToken cancellationToken = default)
    {
        var courses = await _context.Enrollments
            .GroupBy(enrollment => enrollment.Course.Title)
            .Select(group => new
            {
                CourseTitle = group.Key,
                EnrollmentCount = group.Count()
            })
            .OrderByDescending(course => course.EnrollmentCount)
            .Take(5)
            .ToListAsync(cancellationToken);

        return Ok(courses);
    }

    [HttpGet("n-plus-one")]
    public async Task<IActionResult> TestNPlusOne(
        CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var report = new List<object>();

        foreach (var student in students)
        {
            var enrollmentCount = await _context.Enrollments
                .AsNoTracking()
                .CountAsync(
                    enrollment => enrollment.StudentId == student.Id,
                    cancellationToken
                );

            Console.WriteLine(
                $"{student.Name}: {enrollmentCount} enrollments"
            );

            report.Add(new
            {
                student.Name,
                EnrollmentCount = enrollmentCount
            });
        }

        return Ok(report);
    }

    [HttpGet("n-plus-one-fixed")]
    public async Task<IActionResult> TestNPlusOneFixed(
        CancellationToken cancellationToken)
    {
        var report = await _context.Students
            .AsNoTracking()
            .Select(student => new
            {
                student.Name,
                EnrollmentCount = student.Enrollments.Count
            })
            .ToListAsync(cancellationToken);

        foreach (var student in report)
        {
            Console.WriteLine(
                $"{student.Name}: {student.EnrollmentCount} enrollments"
            );
        }

        return Ok(report);
    }

    [HttpPut("students/{id}/audit-test")]
    public async Task<IActionResult> TestAuditUpdate(
        int id,
        CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .SingleOrDefaultAsync(
                student => student.Id == id,
                cancellationToken
            );

        if (student is null)
        {
            return NotFound();
        }

        student.Name = student.Name + " Updated";

        _context.Entry(student)
            .Property("LastUpdated")
            .CurrentValue = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var lastUpdated = _context.Entry(student)
            .Property<DateTime>("LastUpdated")
            .CurrentValue;

        return Ok(new
        {
            student.Id,
            student.Name,
            LastUpdated = lastUpdated
        });
    }

    [HttpPut("students/{id}/concurrency-test")]
    public async Task<IActionResult> TestConcurrency(
        int id,
        [FromServices] IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        // Simulate Staff Member A using one DbContext.
        using var scopeA = scopeFactory.CreateScope();

        var contextA = scopeA.ServiceProvider
            .GetRequiredService<TmsDbContext>();

        // Simulate Staff Member B using a separate DbContext.
        using var scopeB = scopeFactory.CreateScope();

        var contextB = scopeB.ServiceProvider
            .GetRequiredService<TmsDbContext>();

        // Both staff members load the same original database row.
        var studentA = await contextA.Students
            .SingleOrDefaultAsync(
                student => student.Id == id,
                cancellationToken
            );

        var studentB = await contextB.Students
            .SingleOrDefaultAsync(
                student => student.Id == id,
                cancellationToken
            );

        if (studentA is null || studentB is null)
        {
            return NotFound(new
            {
                Message = $"Student {id} was not found."
            });
        }

        // Staff Member A changes the student's name and saves first.
        studentA.Name = studentA.Name + " - Updated by A";

        contextA.Entry(studentA)
            .Property("LastUpdated")
            .CurrentValue = DateTime.UtcNow;

        await contextA.SaveChangesAsync(cancellationToken);

        // Staff Member B still has the older Version value.
        studentB.GPA = Math.Min(4.0m, studentB.GPA + 0.1m);

        contextB.Entry(studentB)
            .Property("LastUpdated")
            .CurrentValue = DateTime.UtcNow;

        try
        {
            await contextB.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                Message = "Both updates succeeded. Concurrency protection did not trigger."
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                Message =
                    "Concurrency conflict detected. Another user changed this student before the second update was saved."
            });
        }
    }

    [HttpGet("students-normal")]
    public async Task<IActionResult> GetNormalStudents(
        CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .AsNoTracking()
            .Select(student => new
            {
                student.Id,
                student.Name,
                student.IsDeleted
            })
            .ToListAsync(cancellationToken);

        return Ok(students);
    }

    [HttpGet("students-admin")]
    public async Task<IActionResult> GetAllStudentsForAdmin(
        CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(student => new
            {
                student.Id,
                student.Name,
                student.IsDeleted
            })
            .ToListAsync(cancellationToken);

        return Ok(students);
    }

    [HttpPut("students/{id}/soft-delete")]
    public async Task<IActionResult> SoftDeleteStudent(
        int id,
        CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                student => student.Id == id,
                cancellationToken
            );

        if (student is null)
        {
            return NotFound();
        }

        student.IsDeleted = true;

        _context.Entry(student)
            .Property("LastUpdated")
            .CurrentValue = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            Message = $"Student {id} was soft deleted."
        });
    }

    [HttpPut("enrollments/archive")]
    public async Task<IActionResult> ArchiveOldEnrollments(
        [FromQuery] DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var cutoffUtc = cutoff.UtcDateTime;

        var affectedRows = await _context.Enrollments
            .Where(enrollment =>
                enrollment.EnrolledAt < cutoffUtc &&
                !enrollment.IsArchived)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    enrollment => enrollment.IsArchived,
                    true
                ),
                cancellationToken
            );

        return Ok(new
        {
            ArchivedEnrollmentCount = affectedRows,
            Cutoff = cutoffUtc
        });
    }


}