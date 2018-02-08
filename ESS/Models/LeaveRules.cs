using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ESS.Models
{
    public class LeaveRules
    {
        [Key]
        public int Id { get; set; }
        public string LeaveRule { get; set; }
        public float AllowedCl { get; set; }
    }
}