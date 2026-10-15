using FashionCommerce.Domain.Common;

namespace FashionCommerce.Domain.Entities.Catalog;

/// <summary>Self-referencing category tree, e.g. Men > Shirts > Casual Shirts.</summary>
public class Category : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // SEO
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    // Navigation
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
