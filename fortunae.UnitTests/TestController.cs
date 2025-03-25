// using Microsoft.AspNetCore.Mvc;

// [ApiController]
// [Route("[controller]")]
// public class TestController : ControllerBase
// {
//     private readonly ApplicationDbContext _context;

//     public TestController(ApplicationDbContext context)
//     {
//         _context = context;
//     }

//     [HttpGet]
//     public IActionResult Get()
//     {
//         try
//         {
//             _context.Database.OpenConnection();
//             _context.Database.CloseConnection();
//             return Ok("Database connection successful!");
//         }
//         catch (Exception ex)
//         {
//             return BadRequest($"Connection failed: {ex.Message}");
//         }
//     }
// }