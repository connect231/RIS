using System;

namespace SOS.Models.Lisans
{
    public class LisansContractVm 
    {
        public string ContractNo { get; set; }
        public string? ContractName { get; set; }
        public string AccountTitle { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? FinishDate { get; set; }
        public DateTime? RenewalDate { get; set; }
        public string ContractStatus { get; set; }
        public string? ContractUrl { get; set; }
        
        public int ContractId { get; set; }
        public string? ContractGuidId { get; set; }
        public string? ContractType { get; set; }
        public decimal? TotalAmount { get; set; }
        public bool HasFiles { get; set; }
    }
}

