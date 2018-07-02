using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESS.Models
{
    public class LeaveTypes
    {
        [Key, Column(Order = 0)]
        [StringLength(2)]
        public string CompCode { get; set; }

        public Company Company { get; set; }

        [Key, Column(Order = 1)]
        [StringLength(10)]
        public string WrkGrp { get; set; }

        public WorkGroups WorkGroup { get; set; }

        [Key, Column(Order = 2)]
        [Required]
        [StringLength(2)]
        public string LeaveTypeCode { get; set; }

        [StringLength(50)]
        public string LeaveTypeName { get; set; }

        public bool Active { get; set; }

        //ADDING STATIC MEMBERS TO ELIMINATE MAGIC STRINGS
        public static readonly string CasualLeave = "CL";
        public static readonly string SickLeave = "SL";
        public static readonly string PaidLeave = "PL";
        public static readonly string OptionalLeave = "OH";
        public static readonly string LeaveWithoutPay = "LW";
        public static readonly string CompOff = "CO";
    }
}