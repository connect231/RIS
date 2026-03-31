namespace SOS.Models.MsK.SpModels
{
    public class SSP_VARUNA_SIPARIS_DETAY
    {

        public int LNGKOD { get; set; }
        public DateTime? DeliveryTime { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? PRODUCTSID { get; set; }
        public string? CrmOrderId { get; set; }
        public string? StockId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? LineDiscountRate { get; set; }
        public string? StockUnitType { get; set; }
        public decimal? Tax { get; set; }
        public string? PYPSapId { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Total { get; set; }
        public decimal? NetLineTotalWithTax { get; set; }
        public string? ProductName { get; set; }
        public string? ItemNo { get; set; }
    }
}

