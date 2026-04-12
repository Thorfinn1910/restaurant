using System;

namespace QuanLyNhaHang.Models
{
    public class StockInEntryInput
    {
        public string StockInId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public double Quantity { get; set; }
        public DateTime DateIn { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public string SupplierContact { get; set; } = string.Empty;
    }
}
