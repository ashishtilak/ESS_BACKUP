using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ESS.Models
{
    public class EmpAddress
    {
        [Key]
        [StringLength(10)]
        public string EmpUnqId { get; set; }

        [StringLength(100)]
        public string PreAdd1 { get; set; }

        [StringLength(100)]
        public string PreAdd2 { get; set; }

        [StringLength(50)]
        public string PreAdd3 { get; set; }

        [StringLength(50)]
        public string PreAdd4 { get; set; }

        [StringLength(50)]
        public string PreDistrict { get; set; }

        [StringLength(50)]
        public string PreCity { get; set; }

        [StringLength(50)]
        public string PreState { get; set; }

        [StringLength(6)]
        public string PrePin { get; set; }

        [StringLength(20)]
        public string PrePhone { get; set; }

        [StringLength(20)]
        public string PreResPhone { get; set; }

        [StringLength(70)]
        public string PreEmail { get; set; }
    }
}
