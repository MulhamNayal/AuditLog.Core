using AuditLog.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuditLog.Core.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly ILogger<EmployeesController> _logger;
        private readonly AuditLogContext _context;

        public EmployeesController(AuditLogContext context, ILogger<EmployeesController> logger)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public async Task<IActionResult> Get()
        {
            _context.Employees.ToList();

            _context.Employees.Add(new Employee { FirstName = "John", LastName = "Don", PhoneNumber = "01233334343" });

            await _context.SaveChangesAsync().ConfigureAwait(false);

            return Ok();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id)
        {
            var entity = await _context.Employees.FindAsync(id);

            entity.FirstName = "Test";

            await _context.SaveChangesAsync().ConfigureAwait(false);

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var entity = await _context.Employees.FindAsync(id);

            _context.Employees.Remove(entity);

            await _context.SaveChangesAsync().ConfigureAwait(false);

            return Ok();

        }
    }
}
