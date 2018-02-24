using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;

namespace ESS.Controllers.Api
{
    public class ChangeReleaseController : ApiController
    {
        private ApplicationDbContext _context;

        public ChangeReleaseController()
        {
            _context = new ApplicationDbContext();
        }

        public IHttpActionResult GetReleaseStrategy(
                        string compCode,
            string wrkGrp,
            string unitCode,
            string deptCode,
            string statCode)
        {
            var releaseStrDto = _context.ReleaseStrategy
                .Where(r =>
                    r.CompCode == compCode &&
                    r.WrkGrp == wrkGrp &&
                    r.UnitCode == unitCode &&
                    r.DeptCode == deptCode &&
                    r.StatCode == statCode
                )
                .Select(Mapper.Map<ReleaseStrategies, ReleaseStrategyDto>)
                .ToList();


            foreach (var dto in releaseStrDto)
            {
                var relStrLvl = _context.ReleaseStrategyLevels
                    .Where(r =>
                        r.ReleaseGroupCode == dto.ReleaseGroupCode &&
                        r.ReleaseStrategy == dto.ReleaseStrategy
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

                    dto.ReleaseStrategyLevels.Add(levelDto);
                }


            }

            if (releaseStrDto.Count > 0)
                return Ok(releaseStrDto);
            else
                return BadRequest("Cannot Find any release strategy for this Dept/Station.");
        }


        [HttpPost]
        public IHttpActionResult ChangeReleaseStrategy(string empUnqId, string secCode)
        {
            var emp = _context.Employees.SingleOrDefault(e => e.EmpUnqId == empUnqId);

            if (emp == null)
                return BadRequest("Invalid employee code.");


            emp.SecCode = secCode;

            _context.SaveChanges();

            //TODO: Call API from Attendance server here...

            return Ok();
        }

    }
}
