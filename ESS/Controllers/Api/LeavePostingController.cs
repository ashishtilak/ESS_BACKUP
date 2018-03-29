using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using System.Data.Entity;
using System.Web.Http;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class LeavePostingController : ApiController
    {
        private ApplicationDbContext _context;

        public LeavePostingController()
        {
            _context = new ApplicationDbContext();
        }

        //this will give pending leaves for posting
        public IHttpActionResult GetLeaves(DateTime fromDt, DateTime toDt, bool flag)
        {
            var leaveAppDto = _context.LeaveApplications
                .Include(e => e.Employee)
                .Include(c => c.Company)
                .Include(cat => cat.Categories)
                .Include(w => w.WorkGroup)
                .Include(d => d.Departments)
                .Include(s => s.Stations)
                .Include(u => u.Units)
                .Include(r => r.ReleaseGroup)
                .Include(rs => rs.RelStrategy)
                .Include(l => l.LeaveApplicationDetails)
                .Where(
                    l => l.ReleaseStatusCode == ReleaseStatus.FullyReleased &&
                        l.LeaveApplicationDetails.Any(ld =>
                                (
                                    ld.IsPosted == LeaveApplicationDetails.NotPosted ||
                                    ld.IsPosted == LeaveApplicationDetails.PartiallyPosted ||
                                    (
                                        ld.IsPosted == LeaveApplicationDetails.FullyPosted
                                          &&
                                          (
                                              ld.IsCancellationPosted == false ||
                                              ld.IsCancellationPosted == null
                                          ) &&
                                          ld.Cancelled == true
                                    )

                                ) &&
                                (
                                    (ld.FromDt <= toDt && ld.ToDt >= fromDt) ||
                                    (ld.FromDt >= toDt && ld.ToDt <= fromDt)
                                )
                            )
                        )
                .ToList()
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>);

            return Ok(leaveAppDto);
        }


        //this will give pending leaves for posting
        public IHttpActionResult GetLeaves(DateTime fromDt, DateTime toDt, string postingFlg)
        {
            var leaveAppDto = _context.LeaveApplications
                .Include(e => e.Employee)
                .Include(c => c.Company)
                .Include(cat => cat.Categories)
                .Include(w => w.WorkGroup)
                .Include(d => d.Departments)
                .Include(s => s.Stations)
                .Include(u => u.Units)
                .Include(r => r.ReleaseGroup)
                .Include(rs => rs.RelStrategy)
                .Include(l => l.LeaveApplicationDetails)
                .Where(l => l.ReleaseStatusCode == ReleaseStatus.FullyReleased &&
                            l.LeaveApplicationDetails.Any(
                                (d =>
                                    (
                                        (d.FromDt <= toDt && d.ToDt >= fromDt) ||
                                        (d.FromDt >= toDt && d.ToDt <= fromDt)
                                    ) && d.IsPosted == postingFlg
                                )
                            )
                    )
                .ToList()
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>);

            return Ok(leaveAppDto);
        }

        public IHttpActionResult PostLeaves([FromBody] object requestData)
        {
            var leavePosting = JsonConvert.DeserializeObject<List<LeavePostingDto>>(requestData.ToString());
            if (!ModelState.IsValid)
                return BadRequest();

            try
            {
                using (var transaction = _context.Database.BeginTransaction())
                {
                    foreach (var dto in leavePosting)
                    {
                        var dto1 = dto;
                        var leave = _context.LeaveApplications
                            .Include(l => l.LeaveApplicationDetails)
                            .Where(l => l.LeaveAppId == dto1.LeaveAppId && l.YearMonth == dto1.YearMonth)
                            .ToList();

                        foreach (var l in leave)
                        {

                            foreach (var leaveApplication in l.LeaveApplicationDetails)
                            {
                                if (leaveApplication.LeaveAppId == dto1.LeaveAppId &&
                                    leaveApplication.LeaveAppItem == dto1.LeaveAppItem &&
                                    leaveApplication.YearMonth == dto1.YearMonth)
                                {
                                    leaveApplication.IsPosted = dto1.IsPosted;
                                    l.UpdUser = dto1.UserId;
                                    l.UpdDt = DateTime.Now;

                                    // If this leave is a cancelled leave, which was fully posted previously,
                                    // we'll set the IsCancellationPosted flag

                                    if (leaveApplication.ParentId != 0 && leaveApplication.Cancelled == true)
                                    {
                                        leaveApplication.IsCancellationPosted = true;
                                    }


                                    if (dto1.IsPosted == LeaveApplicationDetails.PostingRejected)
                                    {
                                        l.Remarks = "HR: " + dto1.Remarks;

                                        //Change release status to "R"

                                        l.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                                        l.UpdUser = dto1.UserId;
                                        l.UpdDt = DateTime.Now;

                                        var appRelStat = _context.ApplReleaseStatus
                                            .Where(
                                                la =>
                                                    la.YearMonth == l.YearMonth &&
                                                    la.ReleaseGroupCode == l.ReleaseGroupCode &&
                                                    la.ApplicationId == l.LeaveAppId
                                            )
                                            .ToList();

                                        //also update app release table
                                        foreach (var b in appRelStat)
                                        {
                                            b.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                                        }
                                    }
                                }
                            }
                        }

                        _context.SaveChanges();
                    }

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }

            return Ok();
        }
    }
}
