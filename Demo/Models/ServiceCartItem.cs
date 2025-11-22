public class ServiceCartItem
{
    public required Service Service { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal => Service.Price * Quantity;
}