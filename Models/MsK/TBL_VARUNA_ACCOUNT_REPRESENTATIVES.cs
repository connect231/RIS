using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOS.Models.MsK;

[Table("TBL_VARUNA_ACCOUNT_REPRESENTATIVES")]
public class TBL_VARUNA_ACCOUNT_REPRESENTATIVES
{
    [Key]
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AccountOwnerId { get; set; }
    public Guid? CompanyId { get; set; }

    [StringLength(100)]
    public string? Type { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    public Guid? EnterpriceAccountRepresentativeId { get; set; }
    public Guid? SalesSDROwnerId { get; set; }
    public Guid? SalesParamAccountRepresentativeId { get; set; }
}
