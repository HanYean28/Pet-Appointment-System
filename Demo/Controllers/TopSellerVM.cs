namespace PawfectGrooming.Models;

public class TopSellerVM
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = "/images/noimage.png";
    public int Count { get; set; }
    public string ItemType { get; set; } = "Service"; // "Service" or "Package"

    // NEW: description and pre-formatted features string (e.g. "Full Service + Luxury Products + Nail service")
    public string Description { get; set; } = string.Empty;
    public string FeaturesFormatted { get; set; } = string.Empty;
}