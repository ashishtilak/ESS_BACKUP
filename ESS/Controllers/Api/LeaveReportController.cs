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

namespace ESS.Controllers.Api
{
    public class LeaveReportController : ApiController
    {
        private ApplicationDbContext _context;

        public LeaveReportController()
        {
            _context = new ApplicationDbContext();
        }


        public IHttpActionResult GetLeaves(string empUnqId, DateTime fromDt, DateTime toDt)
        {
            var releaseCode = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .Select(r => r.ReleaseCode)
                .ToList();


            var releaseStr = _context.ReleaseStrategyLevels
                .Where(l => releaseCode.Contains(l.ReleaseCode) && l.ReleaseGroupCode == ReleaseGroups.LeaveApplication)
                .Select(l => l.ReleaseStrategy)
                .ToList();

            var leaves = _context.LeaveApplications
                .Include(l => l.LeaveApplicationDetails)
                .Include(l => l.Departments)
                .Include(l => l.Stations)
                .Include(l => l.Categories)
                .Include(l => l.Employee)
                .Where(l =>
                    releaseStr.Contains(l.ReleaseStrategy) &&
                    l.ReleaseStatusCode != ReleaseStatus.ReleaseRejected &&
                    l.LeaveApplicationDetails.Any(
                        (d =>
                            (d.FromDt <= toDt && d.ToDt >= fromDt) ||
                            (d.FromDt >= toDt && d.ToDt <= fromDt)
                        )
                    )
                )
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>)
                .ToList();


            return Ok(leaves);
        }

        public IHttpActionResult GetLeaves(string empUnqId, bool employee)
        {
            return Ok();
        }
    }
}
