using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce_system.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        public string? Description { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
        
        // Flash Sale properties
        public bool IsFlashSale { get; set; }
        public int DiscountPercentage { get; set; } // e.g. 20 means 20% off
        public DateTime? FlashSaleEndTime { get; set; }

        [NotMapped]
        public decimal DiscountedPrice => IsFlashSale && DiscountPercentage > 0
            ? Price - (Price * DiscountPercentage / 100)
            : Price;
        
        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }
        
        // Gender classification
        public string? Gender { get; set; } // Male, Female, Other
    }

    public class Category
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string? ImageUrl { get; set; }
        public virtual ICollection<Product> Products { get; set; }
    }

    public class Banner
    {
        [Key]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        [Required]
        public string ImageUrl { get; set; }
        public string? TargetUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HomeViewModel
    {
        public IEnumerable<Product> Products { get; set; }
        public IEnumerable<Category> Categories { get; set; }
        public IEnumerable<Product> BestSellingProducts { get; set; }
        public IEnumerable<Product> FlashSaleProducts { get; set; }
        public IEnumerable<Banner> Banners { get; set; }
        public DateTime? GlobalFlashSaleEndTime { get; set; }
        public string? SelectedGender { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    public class ProductListViewModel
    {
        public IEnumerable<Product> Products { get; set; }
        public IEnumerable<Category> Categories { get; set; }
        public int? SelectedCategoryId { get; set; }
        public string? SelectedCategoryName { get; set; }
        public string? SelectedGender { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }
}
