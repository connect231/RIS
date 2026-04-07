using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOS.Models.MsK;

[Table("TBLSOS_CRM_PERSON_ODATA")]
public class TBLSOS_CRM_PERSON_ODATA
{
    [Key]
    [StringLength(64)]
    public string Id { get; set; } = null!;

    [StringLength(256)]
    public string? Name { get; set; }

    [StringLength(256)]
    public string? SurName { get; set; }

    [StringLength(512)]
    public string? PersonNameSurname { get; set; }

    [StringLength(256)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? CellPhone { get; set; }

    [StringLength(256)]
    public string? Title { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [StringLength(100)]
    public string? ManagerType { get; set; }

    [StringLength(64)]
    public string? ManagerId { get; set; }

    [StringLength(64)]
    public string? CompanyId { get; set; }

    [StringLength(64)]
    public string? DealerId { get; set; }

    [StringLength(64)]
    public string? RoleId { get; set; }

    public DateTime SyncTarihi { get; set; }
}
