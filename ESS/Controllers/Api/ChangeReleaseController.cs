using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.Entity;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class ChangeReleaseController : ApiController
    {
        private ApplicationDbContext _context;

        public ChangeReleaseController()
        {
            _context = new ApplicationDbContext();
        }


        public class ReleaseAuthEmp
        {
            public string ReleaseCode { get; set; }
            public string EmpUnqId { get; set; }
            public string EmpName { get; set; }

        }

        public IHttpActionResult GetReleaseAuth(string empUnqId)
        {
            //first get the release auth for this employee
            var relAuth = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .ToList();

            //list of result to be returned
            List<ReleaseAuthEmp> result = new List<ReleaseAuthEmp>();


            //for each release auth of this employee,
            //get the release codes
            foreach (var auth in relAuth)
            {
                var employees = _context.ReleaseAuth
                    .Where(r => r.ReleaseCode == auth.ReleaseCode)
                    .Select(e => e.EmpUnqId)
                    .ToList();

                // now we have multiple employees against release code (if exist)
                foreach (var emp in employees)
                {
                    ReleaseAuthEmp res = new ReleaseAuthEmp
                    {
                        ReleaseCode = auth.ReleaseCode,
                        EmpUnqId = emp
                    };

                    var empname = _context.Employees.SingleOrDefault(e => e.EmpUnqId == emp);
                    if (empname != null)
                        res.EmpName = empname.EmpName;

                    result.Add(res);

                }
            }

            return Ok(result);
        }


        [HttpPost]
        public IHttpActionResult ChangeReleaseStrategy([FromBody] object requestData)
        {

            var dto = JsonConvert.DeserializeObject<ReleaseStrategyDto>(requestData.ToString());

            //check if release strategy exist. If exist, do nothing,
            //if not, create new release strategy object and add to context

            ReleaseStrategies releaseStrategy = _context.ReleaseStrategy
                                                    .SingleOrDefault(r =>
                                                        r.ReleaseGroupCode == dto.ReleaseGroupCode &&
                                                        r.ReleaseStrategy == dto.ReleaseStrategy
                                                    );

            if (releaseStrategy == null)
            {
                releaseStrategy = new ReleaseStrategies
                {
                    ReleaseGroupCode = dto.ReleaseGroupCode,
                    ReleaseStrategy = dto.ReleaseStrategy,
                    ReleaseStrategyName = dto.ReleaseStrategyName,
                    IsHod = false,
                    Active = true
                };

                _context.ReleaseStrategy.Add(releaseStrategy);

            }

            var releaseStrategyLevels = _context.ReleaseStrategyLevels
                .Where(rl =>
                    rl.ReleaseGroupCode == dto.ReleaseGroupCode &&
                    rl.ReleaseStrategy == dto.ReleaseStrategy
                ).ToList();


            _context.ReleaseStrategyLevels.RemoveRange(releaseStrategyLevels);


            var last = dto.ReleaseStrategyLevels.LastOrDefault();

            foreach (var level in dto.ReleaseStrategyLevels)
            {
                ReleaseStrategyLevels newRelStrLevel =
                    new ReleaseStrategyLevels
                    {
                        ReleaseGroupCode = dto.ReleaseGroupCode,
                        ReleaseStrategy = dto.ReleaseStrategy,
                        ReleaseStrategyLevel = level.ReleaseStrategyLevel,
                        ReleaseCode = level.ReleaseCode,
                        //check if this is last line, in that case 
                        IsFinalRelease = (last != null && last.Equals(level))
                    };
                _context.ReleaseStrategyLevels.Add(newRelStrLevel);
            }

            _context.SaveChanges();

            return Ok();
        }

    }
}
