
using Microsoft.AspNetCore.Mvc;
using TmsApi.Data;

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
}