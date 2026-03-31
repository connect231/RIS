using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Keyless]
public partial class VIEW_N4BISSUESLIFECYCLE
{
    public int? IssueLifeIssueId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ActionEndDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ActionStartDate { get; set; }

    [StringLength(50)]
    public string? EscalationTime { get; set; }

    [StringLength(50)]
    public string? EndDate { get; set; }

    public int? IssueLifeCycleID { get; set; }

    [StringLength(50)]
    public string? StartDate { get; set; }

    public string? IssuerDescription { get; set; }

    [StringLength(50)]
    public string? ResolutionStatusID { get; set; }

    [StringLength(50)]
    public string? ResolutionStatusName { get; set; }

    [StringLength(50)]
    public string? GroupID { get; set; }

    [StringLength(50)]
    public string? UserGroupID { get; set; }

    [StringLength(50)]
    public string? UserGroupName { get; set; }

    [StringLength(50)]
    public string? UserID { get; set; }

    [StringLength(50)]
    public string? UserName { get; set; }

    [StringLength(50)]
    public string? UnitName { get; set; }

    public int? Tarihce_Sira { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Sonislemtarih { get; set; }

    [StringLength(512)]
    public string? Bidirim_BEkletme_Neden { get; set; }

    public byte? Islendi { get; set; }
}

