using DAL.Entities.EFCore.TableSplitting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EFCore.TableSplitting.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        private readonly TableSplittingContext _context;

        public TestController(TableSplittingContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Test()
        {
            return Ok
            (
                new
                {
                    simpleFilesNoInclude = await _context.SimpleFiles.AsNoTracking().ToListAsync(),
                    simpleFilesIncludingDetails = await _context.SimpleFiles.AsNoTracking().Include(e => e.FileDetails).ToListAsync(),
                    detailedFiles = await _context.File.AsNoTracking().ToListAsync()
                }
            );
        }
    }
}
