using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESS.Models
{
    public class Employees
    {

        [Key]
        [StringLength(10)]
        public string EmpUnqId { get; set; }

        [StringLength(2)]
        public string CompCode { get; set; }
        [ForeignKey("CompCode")]
        public Company Company { get; set; }

        [StringLength(10)]
        public string WrkGrp { get; set; }
        [ForeignKey("CompCode, WrkGrp")]
        public WorkGroups WorkGroup { get; set; }

        [StringLength(3)]
        public string EmpTypeCode { get; set; }
        [ForeignKey("CompCode, WrkGrp, EmpTypeCode")]
        public EmpTypes EmpTypes { get; set; }

        [StringLength(3)]
        public string UnitCode { get; set; }
        [ForeignKey("CompCode, WrkGrp, UnitCode")]
        public Units Units { get; set; }

        [StringLength(3)]
        public string DeptCode { get; set; }
        [ForeignKey("CompCode, WrkGrp, UnitCode, DeptCode")]
        public Departments Departments { get; set; }

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
        public Categories Categories { get; set; }

        [StringLength(3)]
        public string DesgCode { get; set; }
        [ForeignKey("CompCode, WrkGrp, DesgCode")]
        public Designations Designations { get; set; }

        [StringLength(3)]
        public string GradeCode { get; set; }
        [ForeignKey("CompCode, WrkGrp, GradeCode")]
        public Grades Grades { get; set; }

        [StringLength(50)]
        public string EmpName { get; set; }

        [StringLength(50)]
        public string FatherName { get; set; }

        public bool Active { get; set; }

        //siple roles
        public bool IsReleaser { get; set; }

        public bool IsHrUser { get; set; }

        public bool IsHod { get; set; }

        [StringLength(20)]
        public string Pass { get; set; }
    }
}