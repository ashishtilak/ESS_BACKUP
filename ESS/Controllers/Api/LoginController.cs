using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class LoginController : ApiController
    {
        private ApplicationDbContext _context;

        public LoginController()
        {
            _context = new ApplicationDbContext();
        }

        [HttpPost]
        public IHttpActionResult GetLogin([FromBody] object requestData)
        {
            var loginDto = JsonConvert.DeserializeObject<LoginDto>(requestData.ToString());

            if (!ModelState.IsValid)
                return BadRequest();

            //Now check if ID is passed, return bad request if id is also passed
            if (string.IsNullOrEmpty(loginDto.EmpUnqId)) return BadRequest("User ID???");
            if (string.IsNullOrEmpty(loginDto.Pass)) return BadRequest("User ID???");

            var employeeDto = _context.Employees
                .Select(
                    e => new EmployeeDto
                    {
                        EmpUnqId = e.EmpUnqId,
                        EmpName = e.EmpName,
                        FatherName = e.FatherName,
                        Active = e.Active,
                        Pass = e.Pass,

                        CompCode = e.CompCode,
                        WrkGrp = e.WrkGrp,
                        UnitCode = e.UnitCode,
                        DeptCode = e.DeptCode,
                        StatCode = e.StatCode,
                        SecCode = e.SecCode,
                        CatCode = e.CatCode,
                        EmpTypeCode = e.EmpTypeCode,
                        GradeCode = e.GradeCode,
                        DesgCode = e.DesgCode,
                        IsReleaser = e.IsReleaser,
                        IsHrUser = e.IsHrUser,
                        IsHod = e.IsHod,

                        CompName = e.Company.CompName,
                        WrkGrpDesc = e.WorkGroup.WrkGrpDesc,
                        UnitName = e.Units.UnitName,
                        DeptName = e.Departments.DeptName,
                        StatName = e.Stations.StatName,
                        SecName = e.Sections.SecName,
                        CatName = e.Categories.CatName,
                        EmpTypeName = e.EmpTypes.EmpTypeName,
                        GradeName = e.Grades.GradeName,
                        DesgName = e.Designations.DesgName
                    }
                )
                .Where(e => e.EmpUnqId == loginDto.EmpUnqId && e.Pass == loginDto.Pass)
                .ToList();

            if (employeeDto.Count == 0)
                return BadRequest("Incorrect password/employee code.");

            return Ok(employeeDto);

        }
    }
}
