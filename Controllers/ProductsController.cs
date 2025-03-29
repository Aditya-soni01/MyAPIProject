using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;    
using MyAPIProject.Data;
using MyAPIProject.Models;
using MyAPIProject.Exceptions;
using Microsoft.AspNetCore.RateLimiting;

namespace MyAPIProject.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
            //_logger = logger;
        }

        [HttpGet]
        [EnableRateLimiting("fixed")]

        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            try
            {
                return await _context.Products.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products");
                throw new ApiException("Error retrieving products", 500);
            }
        }

        [HttpGet("{id}")]
        [EnableRateLimiting("fixed")]

        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                throw new ApiException("Product not found", 404);
            return product;
        }

        [HttpPost]
        [EnableRateLimiting("fixed")]
        public async Task<ActionResult<Product>> CreateProduct(Product product)
        {
            try
            {
                if (!ModelState.IsValid)
                    throw new ApiException("Invalid product data", 400);

                product.CreatedDate = DateTime.UtcNow;
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                throw new ApiException("Error creating product", 500);
            }
        }

        [HttpPut("{id}")]
        [EnableRateLimiting("fixed")]
        public async Task<IActionResult> UpdateProduct(int id, Product product)
        {
            if (id != product.Id)
                return BadRequest();

            _context.Entry(product).State = EntityState.Modified;  // Fixed namespace reference
            
            try 
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Products.AnyAsync(p => p.Id == id))
                    return NotFound();
                throw;
            }
            
            return NoContent();
        }

        [HttpDelete("{id}")]
        [EnableRateLimiting("fixed")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
