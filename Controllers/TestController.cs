
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

}