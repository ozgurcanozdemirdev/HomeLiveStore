using HomeLiveStore.Application.DTOs;
using HomeLiveStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Api.Controllers
{
  [ApiController]
  public sealed class CatalogController(ICatalogService catalogService) : ControllerBase
  {
    [HttpGet("api/categories")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<IReadOnlyList<CategoryListDto>>> Categories(CancellationToken cancellationToken)
    {
      return Ok(await catalogService.GetCategoriesAsync(false, cancellationToken));
    }

    [HttpGet("api/products")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<ProductPageResponse>> Products([FromQuery] string search = "", [FromQuery] int categoryId = 0, [FromQuery] string sort = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken cancellationToken = default)
    {
      return Ok(await catalogService.GetProductsAsync(search, categoryId, sort, page, pageSize, false, cancellationToken));
    }

    [HttpGet("api/products/{id:int}")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<ProductListDto>> Product(int id, CancellationToken cancellationToken)
    {
      return Ok(await catalogService.GetProductAsync(id, false, cancellationToken));
    }

    [HttpGet("api/admin/categories")]
    [Authorize(Roles = "Manager")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<IReadOnlyList<CategoryListDto>>> AdminCategories(CancellationToken cancellationToken)
    {
      return Ok(await catalogService.GetCategoriesAsync(true, cancellationToken));
    }

    [HttpPost("api/admin/categories")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<CategoryListDto>> CreateCategory(CategorySaveDto request, CancellationToken cancellationToken)
    {
      var category = await catalogService.CreateCategoryAsync(request, cancellationToken);
      return Created($"/api/admin/categories/{category.Id}", category);
    }

    [HttpPut("api/admin/categories/{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<CategoryListDto>> UpdateCategory(int id, CategorySaveDto request, CancellationToken cancellationToken)
    {
      return Ok(await catalogService.UpdateCategoryAsync(id, request, cancellationToken));
    }

    [HttpDelete("api/admin/categories/{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
    {
      await catalogService.DeleteCategoryAsync(id, cancellationToken);
      return NoContent();
    }

    [HttpGet("api/admin/products")]
    [Authorize(Roles = "Manager")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<ProductPageResponse>> AdminProducts([FromQuery] string search = "", [FromQuery] int categoryId = 0, [FromQuery] string sort = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken cancellationToken = default)
    {
      return Ok(await catalogService.GetProductsAsync(search, categoryId, sort, page, pageSize, true, cancellationToken));
    }

    [HttpPost("api/admin/products")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<ProductListDto>> CreateProduct(ProductSaveDto request, CancellationToken cancellationToken)
    {
      var product = await catalogService.CreateProductAsync(request, cancellationToken);
      return Created($"/api/products/{product.Id}", product);
    }

    [HttpPut("api/admin/products/{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<ProductListDto>> UpdateProduct(int id, ProductSaveDto request, CancellationToken cancellationToken)
    {
      return Ok(await catalogService.UpdateProductAsync(id, request, cancellationToken));
    }

    [HttpDelete("api/admin/products/{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken)
    {
      await catalogService.DeleteProductAsync(id, cancellationToken);
      return NoContent();
    }
  }
}
