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

        public IHttpActionResult GetReleaseStrategy(
            string compCode,
            string wrkGrp,
            string unitCode,
            string deptCode,
            string statCode,
            string secCode,
            string catCode,
            bool isHod,
            string releaseGroup
            )
        {
            var releaseStrDto = _context.ReleaseStrategy
                .Where(r =>
                    r.CompCode == compCode &&
                    r.WrkGrp == wrkGrp &&
                    r.UnitCode == unitCode &&
                    r.DeptCode == deptCode &&
                    r.StatCode == statCode &&
                    r.SecCode == secCode &&
                    //r.CatCode == catCode &&
                    r.IsHod == isHod &&
                    r.ReleaseGroupCode == releaseGroup
                )
                .Select(Mapper.Map<ReleaseStrategies, ReleaseStrategyDto>)
                .Single();


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
                    .Single(r => r.ReleaseCode == relCode);

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
    }
}
