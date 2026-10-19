using HomeLiveStore.Application.DTOs;

namespace HomeLiveStore.Application.Interfaces.Services
{
  public interface ICatalogService
  {
    Task<IReadOnlyList<CategoryListDto>> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<CategoryListDto> CreateCategoryAsync(CategorySaveDto request, CancellationToken cancellationToken = default);
    Task<CategoryListDto> UpdateCategoryAsync(int id, CategorySaveDto request, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductPageResponse> GetProductsAsync(string search, int categoryId, string sort, int page, int pageSize, bool includeInactive, CancellationToken cancellationToken = default);
    Task<ProductListDto> GetProductAsync(int id, bool includeInactive, CancellationToken cancellationToken = default);
    Task<ProductListDto> CreateProductAsync(ProductSaveDto request, CancellationToken cancellationToken = default);
    Task<ProductListDto> UpdateProductAsync(int id, ProductSaveDto request, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(int id, CancellationToken cancellationToken = default);
  }
}
