using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class EmployeeController : ApiController
    {
        private ApplicationDbContext _context;

        public EmployeeController()
        {
            _context = new ApplicationDbContext();
        }


        //get /api/employee
        public IHttpActionResult GetEmployees()
        {

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


                        CompName = e.Company.CompName,
                        WrkGrpDesc = e.WorkGroup.WrkGrpDesc,
                        UnitName = e.Units.UnitName,
                        DeptName = e.Departments.DeptName,
                        StatName = e.Stations.StatName,
                        SecName = e.Sections.SecName,
                        CatName = e.Categories.CatName,
                        EmpTypeName = e.EmpTypes.EmpTypeName,
                        GradeName = e.Grades.GradeName,
                        DesgName = e.Designations.DesgName,

                        IsHod = e.IsHod,
                        IsHrUser = e.IsHrUser,
                        IsReleaser = e.IsReleaser
                    }
                )
                .ToList()
                .Take(100);

            return Ok(employeeDto);
        }

        public IHttpActionResult GetEmployee(string empUnqId)
        {
            var employee = _context.Employees
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


                        CompName = e.Company.CompName,
                        WrkGrpDesc = e.WorkGroup.WrkGrpDesc,
                        UnitName = e.Units.UnitName,
                        DeptName = e.Departments.DeptName,
                        StatName = e.Stations.StatName,
                        SecName = e.Sections.SecName,
                        CatName = e.Categories.CatName,
                        EmpTypeName = e.EmpTypes.EmpTypeName,
                        GradeName = e.Grades.GradeName,
                        DesgName = e.Designations.DesgName,

                        IsHod = e.IsHod,
                        IsHrUser = e.IsHrUser,
                        IsReleaser = e.IsReleaser
                    }
                )
                .Where(e => e.EmpUnqId == empUnqId)
                .ToList();

            if (employee.Count == 0)
                return NotFound();

            return Ok(employee);
        }


        #region
        // create a temp class for password change
        #endregion

        private class UserPassword
        {
            public string EmpUnqId { get; set; }
            public string Pass { get; set; }
        }


        [HttpPost]
        public IHttpActionResult ChangePassword([FromBody] object requestData)
        {
            var userPassword = JsonConvert.DeserializeObject<UserPassword>(requestData.ToString());

            var employee = _context.Employees
                .Single(e => e.EmpUnqId == userPassword.EmpUnqId);

            if (employee == null)
                return BadRequest("Invalid employee unique Id.");

            employee.Pass = userPassword.Pass;
            _context.SaveChanges();

            return Ok();
        }
    }
}
