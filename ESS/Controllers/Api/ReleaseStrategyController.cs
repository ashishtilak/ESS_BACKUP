using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using System.Data.Entity;

namespace ESS.Controllers.Api
{
    public class ReleaseStrategyController : ApiController
    {
        private ApplicationDbContext _context;

        public ReleaseStrategyController()
        {
            _context = new ApplicationDbContext();
        }


        [HttpGet]
        public IHttpActionResult GetReleaseStrategy(
            string compCode,
            string wrkGrp,
            string unitCode,
            string deptCode,
            string statCode,
            string secCode,
            string catCode,
            bool isHod,
            string releaseGroup,
            string empUnqId
            )
        {
            var releaseStrDto = _context.ReleaseStrategy
                .Where(r =>
                    //r.CompCode == compCode &&
                    //r.WrkGrp == wrkGrp &&
                    //r.UnitCode == unitCode &&
                    //r.DeptCode == deptCode &&
                    //r.StatCode == statCode &&
                    //r.SecCode == secCode &&
                    //    //r.CatCode == catCode &&
                    //r.IsHod == isHod &&
                    r.ReleaseStrategy == empUnqId &&
                    r.ReleaseGroupCode == releaseGroup
                )
                .Select(Mapper.Map<ReleaseStrategies, ReleaseStrategyDto>)
                .FirstOrDefault();


            if (releaseStrDto == null)
                return BadRequest("Invalid release strategy/not defined.");


            var relStrLvl = _context.ReleaseStrategyLevels
                .Where(r =>
                    r.ReleaseGroupCode == releaseStrDto.ReleaseGroupCode &&
                    r.ReleaseStrategy == releaseStrDto.ReleaseStrategy
                )
                .Select(Mapper.Map<ReleaseStrategyLevels, ReleaseStrategyLevelDto>)
                .ToList();

            foreach (var levelDto in relStrLvl)
            {
                var relCode = levelDto.ReleaseCode;
                var releser = _context.ReleaseAuth
                    .FirstOrDefault(r => r.ReleaseCode == relCode);

                if (releser == null)
                    return BadRequest("No one is authorized to release!");

                var emp = _context.Employees
                    .Select(e => new EmployeeDto
                    {
                        EmpUnqId = e.EmpUnqId,
                        EmpName = e.EmpName
                    })
                    .Single(e => e.EmpUnqId == releser.EmpUnqId);

                levelDto.EmpUnqId = emp.EmpUnqId;
                levelDto.EmpName = emp.EmpName;

                releaseStrDto.ReleaseStrategyLevels.Add(levelDto);
            }

            return Ok(releaseStrDto);
        }



        [HttpGet]
        public IHttpActionResult GetReleaseStrategy(string empUnqId)
        {
            //get employee details
            var emp = _context.Employees.Single(e => e.EmpUnqId == empUnqId);
            if (emp == null)
                return BadRequest("Invalid employee code.");

            //return if employee is not a releaser
            if (!emp.IsReleaser)
                return BadRequest("Employee is not authorized to release (check flag).");

            //if he's a releaser, get his release code
            //and based on the code, get all his release strategy levels

            var relCode = _context.ReleaseAuth.Where(r => r.EmpUnqId == emp.EmpUnqId).ToList();

            //create blank employee list for output
            List<EmployeeDto> employees = new List<EmployeeDto>();


            //loop through all release codes found (ideally it should be only one)
            foreach (var releaseAuth in relCode)
            {
                //find all release strategies to which this code belongs
                var relStrategyLevel = _context.ReleaseStrategyLevels
                    .Include(r => r.ReleaseStrategies)
                    .Where(r => r.ReleaseCode == releaseAuth.ReleaseCode)
                    .ToList();


                var relStrategy = relStrategyLevel.Select(level => level.ReleaseStrategies).ToList();

                //and for each strategy we found above,
                //search for employee who match the release criteria
                foreach (var strategy in relStrategy)
                {
                    var relEmployee = _context.Employees
                        .Where(
                            e =>
                                //e.CompCode == strategy.CompCode &&
                                //e.WrkGrp == strategy.WrkGrp &&
                                //e.UnitCode == strategy.UnitCode &&
                                //e.DeptCode == strategy.DeptCode &&
                                //e.StatCode == strategy.StatCode &&
                                //e.SecCode == strategy.SecCode &&
                                //e.IsHod == strategy.IsHod &&
                                e.EmpUnqId == strategy.ReleaseStrategy &&
                                strategy.Active
                        )
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
                        .ToList();

                    //add all above employees to our output list
                    employees.AddRange(relEmployee);
                }
            }

            //if there're any employee, return them
            if (employees.Count == 0)
                return BadRequest("No employee found...");

            return Ok(employees);

        }

    }
}
