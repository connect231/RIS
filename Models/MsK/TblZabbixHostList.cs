using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniCP.Models.MsK
{
    [Table("TBL_ZABBIX_HOST_LIST")]
    public class TblZabbixHostList
    {
        [Key]
        [Column("HOSTID")]
        public int HostId { get; set; }

        [Column("NAME")]
        public string? Name { get; set; }

        [Column("LNGORTAKPROJEKOD")]
        public int? LngOrtakProjeKod { get; set; }
    }
}
