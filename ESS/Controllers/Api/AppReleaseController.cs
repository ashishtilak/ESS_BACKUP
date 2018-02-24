using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Data.Entity;
using System.Transactions;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class AppReleaseController : ApiController
    {
        private ApplicationDbContext _context;

        public AppReleaseController()
        {
            _context = new ApplicationDbContext();
        }


        // GET /api/apprelease
        public IHttpActionResult GetApplReleaseStatus(string empUnqId)
        {
            //TODO: Remove magic string "I"

            var relAuth = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .Select(r => r.ReleaseCode)
                .ToArray();


            var app = _context.ApplReleaseStatus
                .Where(l => relAuth.Contains(l.ReleaseCode) && l.ReleaseStatusCode == "I")
                .ToList();

            var appIds = app.Select(a => a.ApplicationId).ToArray();

            var lv = _context.LeaveApplications
                .Include(l => l.LeaveApplicationDetails)
                .Include(e => e.Employee)
                .Include(c => c.Company)
                .Include(cat => cat.Categories)
                .Include(w => w.WorkGroup)
                .Include(d => d.Departments)
                .Include(s => s.Stations)
                .Include(s => s.Sections)
                .Include(u => u.Units)
                .Include(r => r.ReleaseGroup)
                .Include(rs => rs.RelStrategy)
                .Include(a => a.ApplReleaseStatus)
                .Where(l => appIds.Contains(l.LeaveAppId))
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>)
                .ToList();


            foreach (var dto in lv)
            {
                var appl = _context.ApplReleaseStatus
                    .Where(l =>
                        l.YearMonth == dto.YearMonth &&
                        l.ReleaseGroupCode == dto.ReleaseGroupCode &&
                        l.ApplicationId == dto.LeaveAppId)
                    .ToList()
                    .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

                foreach (var applReleaseStatusDto in appl)
                {
                    dto.ApplReleaseStatus.Add(applReleaseStatusDto);
                }


                var employeeDto = _context.Employees
                    .Select(e => new EmployeeDto
                    {
                        EmpUnqId = e.EmpUnqId,
                        EmpName = e.EmpName,
                        FatherName = e.FatherName,
                        Active = e.Active,
                        Pass = e.Pass,

                        CompCode = e.CatCode,
                        WrkGrp = e.WrkGrp,
                        UnitCode = e.UnitCode,
                        DeptCode = e.DeptCode,
                        StatCode = e.StatCode,
                        SecCode = e.SecCode,
                        CatCode = e.CatCode,
                        EmpTypeCode = e.EmpTypeCode,
                        GradeCode = e.GradeCode,
                        DesgCode = e.DesgCode,
                        IsHod = e.IsHod,

                        CompName = e.Company.CompName,
                        WrkGrpDesc = e.WorkGroup.WrkGrpDesc,
                        UnitName = e.Units.UnitName,
                        DeptName = e.Departments.DeptName,
                        StatName = e.Stations.StatName,
                        CatName = e.Categories.CatName,
                        EmpTypeName = e.EmpTypes.EmpTypeName,
                        GradeName = e.Grades.GradeName,
                        DesgName = e.Designations.DesgName
                    })
                    .Single(e => e.EmpUnqId == dto.EmpUnqId);

                dto.Employee = employeeDto;
            }
            return Ok(lv);
        }


        [HttpPost]
        public IHttpActionResult UpdateApplReleaseStatus([FromBody] object requestData, string empUnqId, string releaseStatusCode)
        {
            //Following details will be filled:
            //YearMonth, ReleaseGroupCode, ApplicationId, ReleaseStrategy, ReleaseStrategyLevel, ReleaseCode


            var applicationDetail = JsonConvert.DeserializeObject<ApplReleaseStatus>(requestData.ToString());

            //If releaseStatusCode is not I, we've nothing to do...
            if (applicationDetail.ReleaseStatusCode != ReleaseStatus.InRelease)
                return BadRequest("Application is not in release state.");

            //first verfy if release code is correct based on the relase code
            DateTime today = DateTime.Now;

            var relAuth = _context.ReleaseAuth
                .Single(
                        r =>
                        r.ReleaseCode == applicationDetail.ReleaseCode &&
                        r.EmpUnqId == applicationDetail.ReleaseAuth &&
                        r.Active &&
                        today >= r.ValidFrom &&
                        today <= r.ValidTo
                    );

            if (relAuth == null)
                BadRequest("Invalid releaser code. Check Active, Valid From, Valid to.");

            //releaser is Ok. Now find release strategy level details
            var relStrLevel = _context.ReleaseStrategyLevels
                .Single(
                    r =>
                        r.ReleaseGroupCode == applicationDetail.ReleaseGroupCode &&
                        r.ReleaseStrategy == applicationDetail.ReleaseStrategy &&
                        r.ReleaseStrategyLevel == applicationDetail.ReleaseStrategyLevel &&
                        r.ReleaseCode == applicationDetail.ReleaseCode
                );

            if (relStrLevel == null)
                return BadRequest("Release strategy detail not found:");

            //get the corresponding leave app header
            var leaveApplication = _context.LeaveApplications
                .Single(
                    l =>
                        l.YearMonth == applicationDetail.YearMonth &&
                        l.LeaveAppId == applicationDetail.ApplicationId
                );

            if (leaveApplication == null)
                return BadRequest("Corresponding leave request is not found!");






            using (var transaction = _context.Database.BeginTransaction())
            {
                if (releaseStatusCode == ReleaseStatus.ReleaseRejected)
                {
                    //set this application details status to "R"
                    //we'll not set next level to "I", it'll forever be "N"
                    applicationDetail.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;

                    //now set leave application header release status to "R" as well
                    leaveApplication.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                }
                else if (releaseStatusCode == ReleaseStatus.FullyReleased)
                {

                    //if this is NOT the final release, set next level release status to "I"
                    if (!applicationDetail.IsFinalRelease)
                    {
                        //get next application level object
                        var nextApplicationLevel = _context.ApplReleaseStatus
                            .Single(
                                a =>
                                    a.YearMonth == applicationDetail.YearMonth &&
                                    a.ReleaseGroupCode == applicationDetail.ReleaseGroupCode &&
                                    a.ApplicationId == applicationDetail.ApplicationId &&
                                    a.ReleaseStrategy == applicationDetail.ReleaseStrategy &&
                                    a.ReleaseStrategyLevel == applicationDetail.ReleaseStrategyLevel + 1
                            );

                        if (nextApplicationLevel == null)
                            return BadRequest("This is not final release, and next release not found! Check database.");

                        //change status of next level object
                        nextApplicationLevel.ReleaseStatusCode = ReleaseStatus.InRelease;

                        //also update leave application status to "P"
                        leaveApplication.ReleaseStatusCode = ReleaseStatus.PartiallyReleased;
                    }
                    else
                    {
                        //if this IS final release, then set leave app header status to "F"
                        leaveApplication.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                    }

                    //Finally set this application details status to "F"
                    applicationDetail.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                    applicationDetail.ReleaseDate = today;

                }

                //explicitely change state to modified, because,
                //we've not taken this record from context...
                _context.Entry(applicationDetail).State = EntityState.Modified;

                //finally update database
                int result = _context.SaveChanges();


                transaction.Commit();
            }

            return Ok(leaveApplication);
        }
    }
}
