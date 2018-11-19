﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using System.Data.Entity;
using System.Net.Http;
using System.Text;
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
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>)
                .ToList();

            leaveAppDto.RemoveAll(l => l.LeaveApplicationDetails.Any(
                    ld =>
                        ld.IsPosted == LeaveApplicationDetails.NotPosted &&
                        ld.Cancelled &&
                        ld.ParentId == 0
                )
            );


            foreach (var lApp in leaveAppDto)
            {
                var app = _context.ApplReleaseStatus
                    .Where(l =>
                        l.YearMonth == lApp.YearMonth &&
                        l.ReleaseGroupCode == lApp.ReleaseGroupCode &&
                        l.ApplicationId == lApp.LeaveAppId &&
                        l.IsFinalRelease
                        )
                    .ToList()
                    .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

                foreach (var applReleaseStatusDto in app)
                {
                    lApp.ApplReleaseStatus.Add(applReleaseStatusDto);
                }
            }

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
                            l.Cancelled == false &&
                            l.LeaveApplicationDetails.Any(
                                (d =>
                                    (
                                        (d.FromDt <= toDt && d.ToDt >= fromDt) ||
                                        (d.FromDt >= toDt && d.ToDt <= fromDt)
                                    ) && d.IsPosted == postingFlg
                                )
                            )
                            ||
                            l.ReleaseStatusCode == ReleaseStatus.ReleaseRejected &&
                            l.Cancelled == false &&
                            l.LeaveApplicationDetails.Any(
                                (d =>
                                    (
                                        (d.FromDt <= toDt && d.ToDt >= fromDt) ||
                                        (d.FromDt >= toDt && d.ToDt <= fromDt)
                                    ) && d.IsPosted == postingFlg
                                )
                            )
                )
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>)
                .ToList();


            foreach (var lApp in leaveAppDto)
            {
                var app = _context.ApplReleaseStatus
                    .Where(l =>
                        l.YearMonth == lApp.YearMonth &&
                        l.ReleaseGroupCode == lApp.ReleaseGroupCode &&
                        l.ApplicationId == lApp.LeaveAppId &&
                        l.IsFinalRelease
                    )
                    .ToList()
                    .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

                foreach (var applReleaseStatusDto in app)
                {
                    lApp.ApplReleaseStatus.Add(applReleaseStatusDto);
                }
            }

            return Ok(leaveAppDto);
        }


        //this will give all leaves for export in excel
        //create a temporary class for data in rows only

        private class Leaves
        {
            //header fields
            public int LeaveAppId;
            public string EmpUnqId;
            public string EmpName;
            public string FatherName;
            public string WrkGrpDesc;
            public string CatName;
            public string DeptName;
            public string StatName;
            public string Remarks;
            public string ReleaseStrategy;

            public string LeaveTypeCode;
            public DateTime FromDt;
            public DateTime ToDt;
            public bool HalfDayFlag;
            public float TotalDays;
            public string LeaveReason;

            public string IsPosted;
            public bool Cancelled;
            public int ParentId;
            public bool IsCancellationPosted;
        }

        public IHttpActionResult GetLeaves(DateTime fromDt, DateTime toDt)
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
                            l.Cancelled == false &&
                            l.LeaveApplicationDetails.Any(
                                (d =>
                                    (
                                        (d.FromDt <= toDt && d.ToDt >= fromDt) ||
                                        (d.FromDt >= toDt && d.ToDt <= fromDt)
                                    )
                                )
                            )
                )
                .ToList()
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>);


            List<Leaves> leaves = (from appHdr in leaveAppDto
                                   from appDtl in appHdr.LeaveApplicationDetails
                                   select new Leaves
                                   {
                                       LeaveAppId = appHdr.LeaveAppId,
                                       EmpUnqId = appHdr.EmpUnqId,
                                       EmpName = appHdr.Employee.EmpName,
                                       FatherName = appHdr.Employee.FatherName,
                                       WrkGrpDesc = appHdr.WorkGroup.WrkGrpDesc,
                                       CatName = appHdr.CatCode,
                                       DeptName = appHdr.Departments.DeptName,
                                       StatName = appHdr.Stations.StatName,
                                       Remarks = appHdr.Remarks,
                                       LeaveTypeCode = appDtl.LeaveTypeCode,
                                       FromDt = appDtl.FromDt,
                                       ToDt = appDtl.ToDt,
                                       TotalDays = appDtl.TotalDays,
                                       HalfDayFlag = appDtl.HalfDayFlag,
                                       LeaveReason = appDtl.Remarks,
                                       IsPosted = appDtl.IsPosted,
                                       Cancelled = appDtl.Cancelled,
                                       ParentId = appDtl.ParentId,
                                       IsCancellationPosted = appDtl.IsCancellationPosted
                                   }).ToList();


            // FOR EDUCATION PURPOSE ONLY:
            //============================
            // ABOVE LINQ EXPRESSION CAN BE WRITTEN IN FOR LOOP AS FOLLOWS:
            //
            // BUT ABOVE ONE IS VERY FAST COMPARED TO FOR LOOP
            //
            //foreach (var appHdr in leaveAppDto)
            //{
            //    foreach (var appDtl in appHdr.LeaveApplicationDetails)
            //    {
            //        Leaves l = new Leaves
            //        {
            //            LeaveAppId = appHdr.LeaveAppId,
            //            EmpUnqId = appHdr.EmpUnqId,
            //            EmpName = appHdr.Employee.EmpName,
            //            FatherName = appHdr.Employee.FatherName,
            //            WrkGrpDesc = appHdr.WorkGroup.WrkGrpDesc,
            //            CatName = appHdr.CatCode,
            //            DeptName = appHdr.Departments.DeptName,
            //            StatName = appHdr.Stations.StatName,
            //            Remarks = appHdr.Remarks,
            //            FromDt = appDtl.FromDt,
            //            ToDt = appDtl.ToDt,
            //            TotalDays = appDtl.TotalDays,
            //            HalfDayFlag = appDtl.HalfDayFlag,
            //            LeaveReason = appDtl.Remarks,
            //            Cancelled = appDtl.Cancelled,
            //            ParentId = appDtl.ParentId,
            //            IsCancellationPosted = appDtl.IsCancellationPosted
            //        };
            //        leaves.Add(l);
            //    }
            //}



            return Ok(leaves);
        }


        public class AttdLeavePost
        {
            public int AppID { get; set; }
            public string EmpUnqID { get; set; }
            public DateTime FromDate { get; set; }
            public DateTime ToDate { get; set; }
            public string LeaveTyp { get; set; }
            public bool HalfDay { get; set; }
            public bool PostedFlg { get; set; }
            public string AttdUser { get; set; }
            public string Remarks { get; set; }
            public string ERROR { get; set; }
        }

        [HttpPost]
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
                                // IF does not match then skip line

                                if (leaveApplication.LeaveAppId != dto1.LeaveAppId ||
                                    leaveApplication.LeaveAppItem != dto1.LeaveAppItem ||
                                    leaveApplication.YearMonth != dto1.YearMonth) continue;

                                // CHECK IF HR USER HAS REJECTED THE LEAVE
                                // IF SO, SET STATUS TO REJECT WITH REMARKS
                                // UPDATE STATUS IN LEAVE APPLICATION AND APP RELEASE TABLES

                                if (dto1.IsPosted == LeaveApplicationDetails.PostingRejected)
                                {
                                    l.Remarks = "HR: " + dto1.Remarks;
                                    if (l.ReleaseStatusCode == ReleaseStatus.NotReleased)
                                    {
                                        if ((l.Cancelled ?? true) || l.ParentId != 0)
                                        {
                                            return BadRequest("Self Cancellation of cancelled leave not allowed.");
                                        }

                                        l.Remarks = "Cancelled by Self.";
                                    }


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

                                    leaveApplication.IsPosted = LeaveApplicationDetails.PostingRejected;

                                    //now skip this loop
                                    continue;
                                }


                                //if partial posted, then do not post in attendance
                                //only post fully posted leaves in attendance server.

                                if (dto.IsPosted == LeaveApplicationDetails.PartiallyPosted)
                                {
                                    //UPDATE POSTED FLAGE

                                    if (leaveApplication.LeaveTypeCode == LeaveTypes.SickLeave && leaveApplication.TotalDays > 3)
                                    {

                                        int slcount = 1;
                                        DateTime dt = leaveApplication.FromDt ?? DateTime.Now;

                                        // loop for each day
                                        while (dt <= leaveApplication.ToDt)
                                        {

                                            if (slcount > 3)
                                            {

                                                // post remaining days as lwp

                                                // this day is not holiday/week off
                                                // so post SL on this day...


                                                // if this day is holiday/week off, then skip this day
                                                List<DateTime> holidays =
                                                    ESS.Helpers.CustomHelper.GetHolidays(dt, dt, l.CompCode, l.WrkGrp);
                                                if (holidays.Count != 0)
                                                {
                                                    dt = dt.AddDays(1);
                                                    continue;
                                                }


                                                List<DateTime> weekOffs =
                                                    ESS.Helpers.CustomHelper.GetWeeklyOff(dt, dt, l.EmpUnqId);
                                                if (weekOffs.Count != 0)
                                                {
                                                    dt = dt.AddDays(1);
                                                    continue;
                                                }


                                                //create object to pass
                                                AttdLeavePost attdLeaveObj =
                                                    new AttdLeavePost
                                                    {
                                                        AppID = leaveApplication.LeaveAppId,
                                                        EmpUnqID = l.EmpUnqId,
                                                        FromDate = dt, //will cause error if dates are null
                                                        ToDate = leaveApplication.ToDt ??
                                                                 DateTime.Now.AddDays(-1), //will cause error if dates are null
                                                        LeaveTyp = LeaveTypes.LeaveWithoutPay,
                                                        HalfDay = false,
                                                        PostedFlg = false,
                                                        AttdUser = dto.UserId, //TODO: CHANGE TO ATTENDANCE SYSTEM USER ID
                                                        Remarks = leaveApplication.Remarks,
                                                        ERROR = ""
                                                    };

                                                bool result;

                                                attdLeaveObj = AttdPostLeave(attdLeaveObj, out result);

                                                // if there was error in leave posting,
                                                // save partial  posting flag in ESS
                                                // and return bad request

                                                if (!result)
                                                {

                                                    leaveApplication.IsPosted = dto1.IsPosted;
                                                    l.UpdUser = dto1.UserId;
                                                    l.UpdDt = DateTime.Now;

                                                    _context.SaveChanges();

                                                    return BadRequest("Error: " + attdLeaveObj.ERROR);
                                                }

                                                dt = leaveApplication.ToDt ?? DateTime.Now;
                                            }
                                            else
                                            {
                                                // if this day is holiday/week off, then skip this day
                                                List<DateTime> holidays =
                                                    ESS.Helpers.CustomHelper.GetHolidays(dt, dt, l.CompCode, l.WrkGrp);
                                                if (holidays.Count != 0)
                                                {
                                                    dt = dt.AddDays(1);
                                                    continue;
                                                }


                                                List<DateTime> weekOffs =
                                                    ESS.Helpers.CustomHelper.GetWeeklyOff(dt, dt, l.EmpUnqId);
                                                if (weekOffs.Count != 0)
                                                {
                                                    dt = dt.AddDays(1);
                                                    continue;
                                                }
                                                //


                                                // if this is first SL, set the date as from date
                                                if (slcount == 1)
                                                {
                                                    leaveApplication.FromDt = dt;
                                                }


                                                // if this is third SL, the post all three SL to attendance server
                                                if (slcount == 3)
                                                {
                                                    //create object to pass
                                                    AttdLeavePost attdLeaveObj =
                                                        new AttdLeavePost
                                                        {
                                                            AppID = leaveApplication.LeaveAppId,
                                                            EmpUnqID = l.EmpUnqId,
                                                            FromDate = leaveApplication.FromDt ?? DateTime.Now, //will cause error if dates are null
                                                            ToDate = dt,
                                                            LeaveTyp = leaveApplication.LeaveTypeCode,
                                                            HalfDay = leaveApplication.HalfDayFlag,
                                                            PostedFlg = false,
                                                            AttdUser = dto.UserId, //TODO: CHANGE TO ATTENDANCE SYSTEM USER ID
                                                            Remarks = leaveApplication.Remarks,
                                                            ERROR = ""
                                                        };

                                                    bool result;

                                                    attdLeaveObj = AttdPostLeave(attdLeaveObj, out result);

                                                    // if there was error in leave posting,
                                                    // save partial  posting flag in ESS
                                                    // and return bad request

                                                    if (!result)
                                                    {
                                                        leaveApplication.IsPosted = dto1.IsPosted;
                                                        l.UpdUser = dto1.UserId;
                                                        l.UpdDt = DateTime.Now;

                                                        _context.SaveChanges();

                                                        return BadRequest("Error: " + attdLeaveObj.ERROR);
                                                    }
                                                }
                                            }

                                            // increase the sl count and set next date
                                            slcount++;
                                            dt = dt.AddDays(1);
                                        }

                                    }

                                    leaveApplication.IsPosted = dto1.IsPosted;
                                    l.UpdUser = dto1.UserId;
                                    l.UpdDt = DateTime.Now;
                                }
                                else
                                {
                                    //POST LEAVE IN ATTENDANCE SERVER.
                                    //Call AttendanceServerApi's /api/leavepost method
                                    // 

                                    //DO NOT POST If full leave is cancelled and is being posted.
                                    if (leaveApplication.Cancelled == true && leaveApplication.ParentId == 0)
                                    {
                                        leaveApplication.IsPosted = dto1.IsPosted;
                                        l.UpdUser = dto1.UserId;
                                        l.UpdDt = DateTime.Now;
                                        leaveApplication.IsCancellationPosted = true;
                                        continue;
                                    }


                                    //create object to pass
                                    AttdLeavePost attdLeaveObj =
                                        new AttdLeavePost
                                        {
                                            AppID = leaveApplication.LeaveAppId,
                                            EmpUnqID = l.EmpUnqId,
                                            FromDate =
                                                leaveApplication.FromDt ??
                                                DateTime.Now, //will cause error if dates are null
                                            ToDate =
                                                leaveApplication.ToDt ??
                                                DateTime.Now.AddDays(-1), //will cause error if dates are null
                                            LeaveTyp = leaveApplication.LeaveTypeCode,
                                            HalfDay = leaveApplication.HalfDayFlag,
                                            PostedFlg = false,
                                            AttdUser = dto.UserId, //TODO: CHANGE TO ATTENDANCE SYSTEM USER ID
                                            Remarks = leaveApplication.Remarks,
                                            ERROR = ""
                                        };

                                    //Change From date to To date in case of COff

                                    if (attdLeaveObj.LeaveTyp == LeaveTypes.CompOff)
                                    {
                                        //In case of CompOff, 
                                        leaveApplication.FromDt = leaveApplication.ToDt;
                                        attdLeaveObj.FromDate = attdLeaveObj.ToDate;
                                    }


                                    bool result;

                                    attdLeaveObj = AttdPostLeave(attdLeaveObj, out result);

                                    if (result)
                                    {
                                        //UPDATE POSTED FLAGE

                                        leaveApplication.IsPosted = dto1.IsPosted;
                                        l.UpdUser = dto1.UserId;
                                        l.UpdDt = DateTime.Now;

                                        // If this leave is a cancelled leave, which was fully posted previously,
                                        // we'll set the IsCancellationPosted flag

                                        if (dto1.IsPosted == LeaveApplicationDetails.FullyPosted &&
                                            leaveApplication.Cancelled == true)
                                        {
                                            leaveApplication.IsCancellationPosted = true;
                                        }

                                    }
                                    else
                                    {
                                        return BadRequest("Error: " + attdLeaveObj.ERROR);
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
                Exception realerror = ex;
                while (realerror.InnerException != null)
                    realerror = realerror.InnerException;


                return BadRequest(realerror.ToString());
            }

            return Ok();
        }


        private AttdLeavePost AttdPostLeave(AttdLeavePost attdLeaveObj, out bool output)
        {

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(Helpers.CustomHelper.GetAttendanceServerApi());

                var content = new StringContent(JsonConvert.SerializeObject(attdLeaveObj),
                    Encoding.UTF8, "application/json");

                var responseTask = client.PostAsync("/api/leavepost", content);
                responseTask.Wait();

                var result = responseTask.Result;
                output = result.IsSuccessStatusCode;

                if (result.IsSuccessStatusCode)
                {
                    var readTask = result.Content.ReadAsAsync<AttdLeavePost>();
                    readTask.Wait();

                    var attdLeave = readTask.Result;

                    return attdLeave;

                }
                else
                {
                    var readTask = result.Content.ReadAsAsync<AttdLeavePost>();
                    readTask.Wait();

                    var attdLeave = readTask.Result;
                    // Some error was there, return it without changing posting flags


                    return attdLeave;
                }
            }

        }

    }
}
