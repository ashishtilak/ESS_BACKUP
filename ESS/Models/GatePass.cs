using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ESS.Models
{
    public class GatePass
    {
        [Key]
        public int Id { get; set; }
        public DateTime GatePassDate { get; set; }
        public int GatePassNo { get; set; }
        public int GatePassItem { get; set; }
        public string EmpUnqId { get; set; }

        [StringLength(1)]
        public string Mode { get; set; }
        public string PlaceOfVisit { get; set; }
        public string Reason { get; set; }
        public string AddUser { get; set; }
        public DateTime AddDateTime { get; set; }

        [StringLength(1)]
        public string GatePassStatus { get; set; }
        public DateTime? GateOutDateTime { get; set; }
        public string GateOutUser { get; set; }
        public string GateOutIp { get; set; }
        public DateTime? GateInDateTime { get; set; }
        public string GateInUser { get; set; }
        public string GateInIp { get; set; }


        public static class GatePassStatuses
        {
            public static readonly string New = "N";
            public static readonly string Out = "O";
            public static readonly string In = "I";
            public static readonly string DutyOff = "D";
            public static readonly string ForceClosed = "F";
        }

        public static class GatePassModes
        {
            public static readonly string Official = "O";
            public static readonly string Personal = "P";
            public static readonly string DutyOff = "D";
        }

    }
}