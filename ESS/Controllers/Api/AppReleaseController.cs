using System;
using System.Linq;
using System.Data.Entity;
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
                    var relCode = _context.ReleaseAuth
                        .Where(r => r.ReleaseCode == applReleaseStatusDto.ReleaseCode)
                        .ToList();

                    foreach (var auth in relCode)
                    {
                        if (auth.EmpUnqId == empUnqId)
                            applReleaseStatusDto.ReleaseAuth = empUnqId;
                    }

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
                        //SecCode = e.SecCode,
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


            var dto = JsonConvert.DeserializeObject<ApplReleaseStatus>(requestData.ToString());


            var applicationDetail = _context.ApplReleaseStatus
                .SingleOrDefault(
                    a => a.YearMonth == dto.YearMonth &&
                         a.ReleaseGroupCode == dto.ReleaseGroupCode &&
                         a.ApplicationId == dto.ApplicationId &&
                         a.ReleaseStrategyLevel == dto.ReleaseStrategyLevel
                );

            if (applicationDetail == null)
                return BadRequest("Invalid app release status detals...");

            applicationDetail.Remarks = dto.Remarks;

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
                .Include(l => l.LeaveApplicationDetails)
                .Single(
                    l =>
                        l.YearMonth == applicationDetail.YearMonth &&
                        l.LeaveAppId == applicationDetail.ApplicationId
                );

            if (leaveApplication == null)
                return BadRequest("Corresponding leave request is not found!");



            leaveApplication.Remarks = dto.Remarks;


            using (var transaction = _context.Database.BeginTransaction())
            {
                if (releaseStatusCode == ReleaseStatus.ReleaseRejected)
                {
                    // Check if this is a short cancelled leave being rejected
                    if (leaveApplication.ParentId != 0)
                    {
                        // We'll first restore the original leave application's cancelled flag
                        // and delete the new short leave...

                        var parentLeave = _context.LeaveApplications
                            .Include(l => l.LeaveApplicationDetails)
                            .SingleOrDefault(l =>
                                l.LeaveAppId == leaveApplication.ParentId &&
                                l.ReleaseGroupCode == ReleaseGroups.LeaveApplication
                            );

                        if (parentLeave != null)
                        {
                            parentLeave.ParentId = 0;
                            parentLeave.Cancelled = false;

                            foreach (var detail in parentLeave.LeaveApplicationDetails)
                            {
                                detail.Cancelled = false;
                                detail.ParentId = 0;
                            }

                            //now delete the short leave
                            var appRelease = _context.ApplReleaseStatus.Where
                            (
                                a => a.YearMonth == leaveApplication.YearMonth &&
                                     a.ReleaseGroupCode == leaveApplication.ReleaseGroupCode &&
                                     a.ApplicationId == leaveApplication.LeaveAppId
                            ).ToList();

                            _context.ApplReleaseStatus.RemoveRange(appRelease);
                            _context.LeaveApplicationDetails.RemoveRange(leaveApplication.LeaveApplicationDetails);
                            _context.LeaveApplications.Remove(leaveApplication);
                        }

                    }
                    else
                    {
                        //now check if this is fully cancelled leave or not
                        if (leaveApplication.Cancelled == true)
                        {
                            //reset cancellation flag and release status to "F"
                            leaveApplication.Cancelled = false;
                            leaveApplication.ReleaseStatusCode = ReleaseStatus.FullyReleased;


                            //reset release status to fully released
                            applicationDetail.ReleaseStatusCode = ReleaseStatus.FullyReleased;

                            //reset cancellation flag for leave app details
                            foreach (var detail in leaveApplication.LeaveApplicationDetails)
                            {
                                detail.Cancelled = false;
                            }
                        }
                        else
                        {
                            //set this application details status to "R"
                            //we'll not set next level to "I", it'll forever be "N"
                            applicationDetail.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;

                            //now set leave application header release status to "R" as well
                            leaveApplication.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                        }
                    }
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
                        // DISABLED ON 04.07.2018
                        // NOW SINCE ESS IS LIVE, HR WILL REVIEW AND POST 
                        // FROM ESS TO ATTENDANCE.


                        ////if this IS final release, then set leave app header status to "F"
                        leaveApplication.ReleaseStatusCode = ReleaseStatus.FullyReleased;

                        //foreach (var l in leaveApplication.LeaveApplicationDetails)
                        //{
                        //    l.IsPosted = LeaveApplicationDetails.FullyPosted;
                        //}

                        //If leave is fully cancelled and was not posted,
                        //set IsCancellationPosted flag to remove it from leave posting report 

                        if (leaveApplication.Cancelled == true && leaveApplication.ParentId == 0)
                        {
                            foreach (var detail in leaveApplication.LeaveApplicationDetails)
                            {
                                if (detail.IsPosted == LeaveApplicationDetails.NotPosted)
                                    detail.IsCancellationPosted = true;
                            }

                        }

                    }

                    //Finally set this application details status to "F"
                    applicationDetail.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                    applicationDetail.ReleaseDate = today;

                }

                //explicitely change state to modified, because,
                //we've not taken this record from context...
                //_context.Entry(applicationDetail).State = EntityState.Modified;

                //finally update database
                _context.SaveChanges();


                transaction.Commit();
            }

            return Ok(leaveApplication);
        }
    }
}
