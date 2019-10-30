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
        public IHttpActionResult GetReleaseStrategy(string releaseGroup, string empUnqId)
        {
            if (releaseGroup == ReleaseGroups.LeaveApplication)
            {
                var releaseStrDto = _context.ReleaseStrategy
                    .Where(r =>
                        r.ReleaseStrategy == empUnqId &&
                        r.ReleaseGroupCode == releaseGroup
                    ).ToList()
                    .Select(Mapper.Map<ReleaseStrategies, ReleaseStrategyDto>)
                    .FirstOrDefault();


                if (releaseStrDto == null)
                    return BadRequest("Invalid release strategy/not defined.");


                var relStrLvl = _context.ReleaseStrategyLevels
                    .Where(r =>
                        r.ReleaseGroupCode == releaseStrDto.ReleaseGroupCode &&
                        r.ReleaseStrategy == releaseStrDto.ReleaseStrategy
                    ).ToList()
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
            else if (releaseGroup == ReleaseGroups.GatePass)
            {
                //Get emp details like compcode, wrkgrp....

                var gpEmp = _context.Employees
                    .SingleOrDefault(e => e.EmpUnqId == empUnqId);

                if (gpEmp == null)
                    return BadRequest("Invalid employee");

                var gpReleaseStrDto = _context.GpReleaseStrategy
                    .Where(r =>
                        r.ReleaseGroupCode == releaseGroup &&
                        r.CompCode == gpEmp.CompCode &&
                        r.WrkGrp == gpEmp.WrkGrp &&
                        r.UnitCode == gpEmp.UnitCode &&
                        r.DeptCode == gpEmp.DeptCode &&
                        r.StatCode == gpEmp.StatCode &&
                        r.Active
                    )
                    .Select(Mapper.Map<GpReleaseStrategies, GpReleaseStrategyDto>)
                    .FirstOrDefault();



                List<GpReleaseStrategyLevelDto> relStrLvl = new List<GpReleaseStrategyLevelDto>();

                if (gpReleaseStrDto != null)
                {
                    relStrLvl = _context.GpReleaseStrategyLevels
                        .Where(r =>
                            r.ReleaseGroupCode == gpReleaseStrDto.ReleaseGroupCode &&
                            r.GpReleaseStrategy == gpReleaseStrDto.GpReleaseStrategy
                        )
                        .Select(Mapper.Map<GpReleaseStrategyLevels, GpReleaseStrategyLevelDto>)
                        .ToList();


                }

                // DAY release strategy

                var gpReleaseStrDayDto = _context.GpReleaseStrategy
                    .Where(r =>
                        r.ReleaseGroupCode == releaseGroup &&
                        r.GpReleaseStrategy == empUnqId &&
                        r.Active
                    )
                    .Select(Mapper.Map<GpReleaseStrategies, GpReleaseStrategyDto>)
                    .FirstOrDefault();


                if (gpReleaseStrDto == null && gpReleaseStrDayDto == null)
                    return BadRequest("Invalid release strategy/not defined.");


                var relStrLvlDay = _context.GpReleaseStrategyLevels
                    .Where(r =>
                        r.ReleaseGroupCode == gpReleaseStrDayDto.ReleaseGroupCode &&
                        r.GpReleaseStrategy == gpReleaseStrDayDto.GpReleaseStrategy
                    )
                    .Select(Mapper.Map<GpReleaseStrategyLevels, GpReleaseStrategyLevelDto>)
                    .ToList();


                relStrLvl.AddRange(relStrLvlDay);

                int count = 0;

                foreach (var levelDto in relStrLvl)
                {
                    var relCode = levelDto.ReleaseCode;
                    var releser = _context.ReleaseAuth
                        .Where(r => r.ReleaseCode == relCode)
                        .ToList();

                    if (releser.Count != 0)
                        count += releser.Count;

                    foreach (var r in releser)
                    {

                        var emp = _context.Employees
                            .Select(e => new EmployeeDto
                            {
                                EmpUnqId = e.EmpUnqId,
                                EmpName = e.EmpName
                            })
                            .Single(e => e.EmpUnqId == r.EmpUnqId);

                        GpReleaseStrategyLevelDto relDto =
                            new GpReleaseStrategyLevelDto
                            {
                                ReleaseGroupCode = levelDto.ReleaseGroupCode,
                                GpReleaseStrategy = levelDto.GpReleaseStrategy,
                                GpReleaseStrategyLevel = levelDto.GpReleaseStrategyLevel,
                                ReleaseCode = levelDto.ReleaseCode,
                                IsFinalRelease = levelDto.IsFinalRelease,
                                EmpUnqId = emp.EmpUnqId,
                                EmpName = emp.EmpName,
                                IsGpNightReleaser = r.IsGpNightReleaser
                            };


                        gpReleaseStrDto.GpReleaseStrategyLevels.Add(relDto);
                    }
                }


                if (count == 0)
                    return BadRequest("No one is authorized to release!");

                return Ok(gpReleaseStrDto);

            }
            else if (releaseGroup == ReleaseGroups.GatePassAdvice)
            {
                //For gate pass advice

                var releaseStrDto = _context.GaReleaseStrategies
                    .Where(r =>
                        r.GaReleaseStrategy == empUnqId &&
                        r.ReleaseGroupCode == releaseGroup
                    ).ToList()
                    .Select(Mapper.Map<GaReleaseStrategies, GaReleaseStrategyDto>)
                    .FirstOrDefault();


                if (releaseStrDto == null)
                    return BadRequest("Invalid release strategy/not defined.");


                var relStrLvl = _context.GaReleaseStrategyLevels
                    .Where(r =>
                        r.ReleaseGroupCode == releaseStrDto.ReleaseGroupCode &&
                        r.GaReleaseStrategy == releaseStrDto.GaReleaseStrategy
                    ).ToList()
                    .Select(Mapper.Map<GaReleaseStrategyLevels, GaReleaseStrategyLevelDto>)
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

                    releaseStrDto.GaReleaseStrategyLevels.Add(levelDto);
                }

                return Ok(releaseStrDto);
            }
            else
                return BadRequest("Release strategy group code not found."); //If other that LA/GP is specified, return error
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
                                //SecCode = e.SecCode,
                                CatCode = e.CatCode,
                                EmpTypeCode = e.EmpTypeCode,
                                GradeCode = e.GradeCode,
                                DesgCode = e.DesgCode,


                                CompName = e.Company.CompName,
                                WrkGrpDesc = e.WorkGroup.WrkGrpDesc,
                                UnitName = e.Units.UnitName,
                                DeptName = e.Departments.DeptName,
                                StatName = e.Stations.StatName,
                                //SecName = e.Sections.SecName,
                                CatName = e.Categories.CatName,
                                EmpTypeName = e.EmpTypes.EmpTypeName,
                                GradeName = e.Grades.GradeName,
                                DesgName = e.Designations.DesgName,

                                IsHod = e.IsHod,
                                IsHrUser = e.IsHrUser,
                                IsReleaser = e.IsReleaser,
                                Email = e.Email,

                                Location = e.Location
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
