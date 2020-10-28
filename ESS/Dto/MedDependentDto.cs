using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ESS.Models;

namespace ESS.Dto
{
    public class MedDependentDto
    {
        public string EmpUnqId { get; set; }
        public int DepSr { get; set; }
        public string DepName { get; set; }
        public string Rleation { get; set; }
        public DateTime BirthDate { get; set; }
        public string Gender { get; set; }
        public DateTime? MarriageDate { get; set; }
        public string Pan { get; set; }
        public string Aadhar { get; set; }
        public string BirthCertiicateNo { get; set; }
        public DateTime EffectiveDate { get; set; } //by default add date
        public string ReleaseGroupCode { get; set; }
        public ReleaseGroupDto ReleaseGroup { get; set; }
        public string ReleaseStrategy { get; set; }
        public ReleaseStrategyDto RelStrategy { get; set; }
        public string ReleaseStatusCode { get; set; }
        public DateTime? ReleaseDt { get; set; }
        public string ReleaseUser { get; set; }
        public bool Active { get; set; }
        public string AddUser { get; set; }
        public DateTime AddDate { get; set; }
        public List<ApplReleaseStatusDto> ApplReleaseStatus { get; set; }

        public List<MedEmpUhidDto> UhIds { get; set; }
    }
}