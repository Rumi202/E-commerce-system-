namespace E_commerce_system.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal OriginalPrice { get; set; }
        public int DiscountPercentage { get; set; }
        public bool IsFlashSale { get; set; }
        public int Quantity { get; set; }
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }

        public decimal TotalPrice => UnitPrice * Quantity;
    }
}
