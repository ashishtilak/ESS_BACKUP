using System;
using System.Data.Entity;
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
    public class LeaveBalanceController : ApiController
    {
        private ApplicationDbContext _context;

        public LeaveBalanceController()
        {
            _context = new ApplicationDbContext();
        }

        public IHttpActionResult GetLeaveBalance(string empUnqId, int yearMonth)
        {

            //get leave balance from attendance server
            //note this will not have leaves that are not posted.

            var leaveBalDto = ESS.Helpers.CustomHelper.GetLeaveBalance(empUnqId, yearMonth);

            //now get applied and released leaves that are not posted

            var leaveAppDtl = _context.LeaveApplications
                .Where(l =>
                    l.EmpUnqId == empUnqId &&
                    l.ReleaseStatusCode == ReleaseStatus.FullyReleased &&
                    l.LeaveApplicationDetails.Any(d => d.IsPosted == LeaveApplicationDetails.NotPosted)
                )
                .Include(l => l.LeaveApplicationDetails)
                .ToList();

            foreach (var apps in leaveAppDtl)
            {
                foreach (var details in apps.LeaveApplicationDetails)
                {
                    if (details.IsPosted != LeaveApplicationDetails.NotPosted) continue;

                    try
                    {
                        var l = leaveBalDto.Single(x => x.LeaveTypeCode == details.LeaveTypeCode);

                        if (l != null)
                            l.Availed += details.TotalDays;
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return Ok(leaveBalDto);
        }
    }
}
