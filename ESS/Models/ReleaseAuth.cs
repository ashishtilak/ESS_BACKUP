﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ESS.Models
{
    public class ReleaseAuth
    {
        [Key, Column(Order = 1)]
        [Required]
        [StringLength(20)]
        public string ReleaseCode { get; set; }

        [Key, Column(Order = 2)]
        [Required]
        [StringLength(10)]
        public string EmpUnqId { get; set; }

        [ForeignKey("EmpUnqId")] public Employees Employee { get; set; }

        public DateTime? ValidFrom { get; set; }

        public DateTime? ValidTo { get; set; }

        public bool Active { get; set; }

        public bool IsGpNightReleaser { get; set; }
    }
}