using System.ComponentModel.DataAnnotations;

namespace ESS.Models
{
    public class ReleaseGroups
    {
        [Key]
        [Required]
        [StringLength(2)]
        public string ReleaseGroupCode { get; set; }

        [StringLength(50)]
        public string ReleaseGroupDesc { get; set; }

        public static readonly string LeaveApplication = "LA";
        public static readonly string OutStationDuty = "OD";
        public static readonly string GatePass = "GP";
        public static readonly string GatePassAdvice = "GA"; 
    }
}