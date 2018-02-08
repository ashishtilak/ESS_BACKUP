using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESS.Models
{
    public class ReleaseStrategies
    {
        [Key, Column(Order = 0)]
        [Required]
        [StringLength(15)]
        public string ReleaseGroupCode { get; set; }

        public ReleaseGroups ReleaseGroup { get; set; }

        [Key, Column(Order = 1)]
        [Required]
        [StringLength(15)]
        public string ReleaseStrategy { get; set; }

        [StringLength(100)]
        public string ReleaseStrategyName { get; set; }

        [StringLength(2)]
        public string CompCode { get; set; }

        [ForeignKey("CompCode")]
        public Company Company { get; set; }

        [StringLength(10)]
        public string WrkGrp { get; set; }

        [ForeignKey("CompCode, WrkGrp")]
        public WorkGroups WorkGroup { get; set; }

        [StringLength(3)]
        public string UnitCode { get; set; }

        [ForeignKey("CompCode, WrkGrp, UnitCode")]
        public Units Unit { get; set; }

        [StringLength(3)]
        public string DeptCode { get; set; }

        [ForeignKey("CompCode, WrkGrp, UnitCode, DeptCode")]
        public Departments Department { get; set; }

        [StringLength(3)]
        public string StatCode { get; set; }

        [ForeignKey("CompCode, WrkGrp, UnitCode, DeptCode, StatCode")]
        public Stations Stations { get; set; }

        [StringLength(3)]
        public string SecCode { get; set; }

        [ForeignKey("CompCode, WrkGrp, UnitCode, DeptCode, StatCode, SecCode")]
        public Sections Sections { get; set; }

        [StringLength(3)]
        public string CatCode { get; set; }

        [ForeignKey("CompCode, WrkGrp, CatCode")]
        public Categories Category { get; set; }

        public bool IsHod { get; set; }

        public bool Active { get; set; }

        public List<ReleaseStrategyLevels> ReleaseStrategyLevels;
    }
}