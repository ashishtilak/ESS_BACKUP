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
                        IsReleaser = e.IsReleaser,
                        Email = e.Email
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
                        IsReleaser = e.IsReleaser,
                        Email = e.Email
                    }
                )
                .Where(e => e.EmpUnqId == empUnqId)
                .ToList();

            if (employee.Count == 0)
                return NotFound();

            return Ok(employee);
        }


        private class EmpRelease
        {
            public string EmpUnqId { get; set; }
            public string EmpName { get; set; }
            public string WrkGrp { get; set; }
            public string UnitCode { get; set; }
            public string DeptCode { get; set; }
            public string DeptName { get; set; }
            public string StatCode { get; set; }
            public string StatName { get; set; }
            public string SecCode { get; set; }
            public string SecName { get; set; }
            public string Email { get; set; }
            public string ReleaseGroup { get; set; }
            public string ReleaseStrategy { get; set; }

            public string ReleaseCode1 { get; set; }
            public string ReleaseEmpUnqId1 { get; set; }
            public string ReleaseName1 { get; set; }

            public string ReleaseCode2 { get; set; }
            public string ReleaseEmpUnqId2 { get; set; }
            public string ReleaseName2 { get; set; }

            public string ReleaseCode3 { get; set; }
            public string ReleaseEmpUnqId3 { get; set; }
            public string ReleaseName3 { get; set; }

        }

        public IHttpActionResult GetEmpRelease(bool flag)
        {

            var emps = _context.Employees
                .Include(d => d.Departments)
                .Include(s => s.Stations)
                .Include(e => e.Sections)
                .Where(e => e.Active && e.WrkGrp == "COMP")
                .ToList();

            List<EmpRelease> result = new List<EmpRelease>();

            foreach (var emp in emps)
            {
                EmpRelease e = new EmpRelease
                {
                    EmpUnqId = emp.EmpUnqId,
                    EmpName = emp.EmpName,
                    WrkGrp = emp.WrkGrp,
                    UnitCode = emp.UnitCode
                };

                if (emp.DeptCode != null)
                {
                    e.DeptCode = emp.DeptCode;
                    e.DeptName = emp.Departments.DeptName;
                }

                if (emp.StatCode != null)
                {
                    e.StatCode = emp.StatCode;
                    e.StatName = emp.Stations.StatName;
                }

                if (emp.SecCode != null)
                {
                    e.SecCode = emp.SecCode;
                    e.SecName = emp.Sections.SecName;
                }

                e.Email = emp.Email;

                var relStr = _context.ReleaseStrategy
                    .SingleOrDefault(r =>
                        r.WrkGrp == e.WrkGrp &&
                        r.UnitCode == e.UnitCode &&
                        r.DeptCode == e.DeptCode &&
                        r.StatCode == e.StatCode &&
                        r.SecCode == e.SecCode
                    );

                if (relStr == null)
                {
                    result.Add(e);
                    continue;
                }


                var relStrLvl = _context.ReleaseStrategyLevels
                    .Where(r =>
                        r.ReleaseGroupCode == relStr.ReleaseGroupCode &&
                        r.ReleaseStrategy == relStr.ReleaseStrategy
                    ).ToList();
                foreach (var level in relStrLvl)
                {
                    var relAuth = _context.ReleaseAuth
                        .Include(re => re.Employee)
                        .Where(r => r.ReleaseCode == level.ReleaseCode)
                        .ToList();

                    string relEmpUnqId = "";
                    string relName = "";
                    foreach (var auth in relAuth)
                    {
                        if (relEmpUnqId == "")
                            relEmpUnqId = auth.EmpUnqId;
                        else
                            relEmpUnqId += "/" + auth.EmpUnqId;

                        if (relName == "")
                            relName = auth.Employee.EmpName;
                        else
                            relName += "/" + auth.Employee.EmpName;

                    }

                    e.ReleaseGroup = relStr.ReleaseGroupCode;
                    e.ReleaseStrategy = relStr.ReleaseStrategy;

                    switch (level.ReleaseStrategyLevel)
                    {
                        case 1:
                            e.ReleaseCode1 = level.ReleaseCode;
                            e.ReleaseEmpUnqId1 = relEmpUnqId;
                            e.ReleaseName1 = relName;
                            break;
                        case 2:
                            e.ReleaseCode2 = level.ReleaseCode;
                            e.ReleaseEmpUnqId2 = relEmpUnqId;
                            e.ReleaseName2 = relName;
                            break;
                        case 3:
                            e.ReleaseCode3 = level.ReleaseCode;
                            e.ReleaseEmpUnqId3 = relEmpUnqId;
                            e.ReleaseName3 = relName;
                            break;

                    }


                }

                result.Add(e);
            }


            return Ok(result);
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
        [ActionName("changepassword")]
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


        [HttpPost]
        [ActionName("updateemail")]
        public IHttpActionResult UpdateEmail(string empUnqId, string email)
        {
            var emp = _context.Employees.Single(e => e.EmpUnqId == empUnqId);

            if (emp == null)
                return BadRequest("Invalid employee code");

            emp.Email = email;
            _context.SaveChanges();

            return Ok();
        }
    }
}
