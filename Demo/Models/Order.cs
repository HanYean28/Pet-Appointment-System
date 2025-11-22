using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using PawfectGrooming.Models;

namespace PawfectGrooming.Models
{
    public class Order
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public bool Paid { get; set; }
        public required List<OrderLine> OrderLines { get; set; }
    }

    public class OrderLine
    {
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public required ServiceOption ServiceOption { get; set; }
    }

    public class Product
    {
        public required string PhotoURL { get; set; }
    }
}

