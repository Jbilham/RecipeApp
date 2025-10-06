using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UnitsController : ControllerBase
    {
        private readonly AppDb _db;
        public UnitsController(AppDb db) => _db = db;

        [HttpGet]
        public async Task<IEnumerable<Unit>> Get()
        {
            return await _db.Units.ToListAsync();
        }
    }
}
