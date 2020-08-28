using System;
using System.Linq;
using System.Data.Entity;
using System.Web.Http;
using System.Collections.Generic;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class AppReleaseController : ApiController
    {
        private readonly ApplicationDbContext _context;

        public AppReleaseController()
        {
            _context = new ApplicationDbContext();
        }


        // GET /api/apprelease
        [HttpGet]
        [ActionName("getapplreleasestatus")]
        public IHttpActionResult GetApplReleaseStatus(string empUnqId)
        {
            var relAuth = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .Select(r => r.ReleaseCode)
                .ToArray();


            var app = _context.ApplReleaseStatus
                .Where(l =>
                    relAuth.Contains(l.ReleaseCode) &&
                    l.ReleaseStatusCode == ReleaseStatus.InRelease && (
                        l.ReleaseGroupCode == ReleaseGroups.LeaveApplication  ||
                        l.ReleaseGroupCode == ReleaseGroups.OutStationDuty ||
                        l.ReleaseGroupCode == ReleaseGroups.CompOff
                        )
                )
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
                .Where(l => appIds.Contains(l.LeaveAppId)).ToList()
                .Select(Mapper.Map<LeaveApplications, LeaveApplicationDto>)
                .ToList();


            foreach (LeaveApplicationDto dto in lv)
            {
                var appl = _context.ApplReleaseStatus
                    .Where(l =>
                        l.YearMonth == dto.YearMonth &&
                        l.ReleaseGroupCode == dto.ReleaseGroupCode &&
                        l.ApplicationId == dto.LeaveAppId)
                    .ToList()
                    .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

                foreach (ApplReleaseStatusDto applReleaseStatusDto in appl)
                {
                    var relCode = _context.ReleaseAuth
                        .Where(r => r.ReleaseCode == applReleaseStatusDto.ReleaseCode)
                        .ToList();

                    foreach (ReleaseAuth unused in relCode.Where(auth => auth.EmpUnqId == empUnqId))
                    {
                        applReleaseStatusDto.ReleaseAuth = empUnqId;
                    }

                    dto.ApplReleaseStatus.Add(applReleaseStatusDto);
                }


                EmployeeDto employeeDto = _context.Employees
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
                        DesgName = e.Designations.DesgName,

                        Location = e.Location
                    })
                    .Single(e => e.EmpUnqId == dto.EmpUnqId);

                dto.Employee = employeeDto;
            }

            return Ok(lv);
        }

        private class ScheduleData
        {
            public int YearMonth { get; set; }
            public int ScheduleId { get; set; }
            public string AddUser { get; set; }
            public List<ShiftScheduleDto> Schedules { get; set; }

            public ScheduleData()
            {
                //
            }
        }
        
        [HttpGet]
        [ActionName("getapplreleasestatus")]
        public IHttpActionResult GetApplReleaseStatus(string empUnqId, string releaseGroupCode)
        {
            if (releaseGroupCode == ReleaseGroups.GatePass)
            {
                var relAuth = _context.ReleaseAuth
                    .Where(r => r.EmpUnqId == empUnqId)
                    .ToList();

                var outputGp = new List<GatePassDto>();


                foreach (ReleaseAuth rAuth in relAuth)
                {
                    var app = new List<ApplReleaseStatus>();

                    if (rAuth.IsGpNightReleaser)
                    {
                        // ************************************************************
                        //
                        // GET ALL THE EMPLOYEE FOR WHOME empUnqId IS RELEASER FOR NIGHT!
                        // ALGORITHM:
                        //
                        // Goto ReleaseStrategylevels and pass ReleaseCode of releaser, get GpReleaseStrategy 
                        // Pass GpReleaseStrategy to GpReleaseStrategies table and get Comp,WrkGrp,Unit,Dept,Stat
                        // Get List of all employees belonging to that Comp/Wrk/Unit/Dept/Stat
                        // then pass this list of employees to AppReleaseStatus table with releasestatuscode = 'I'
                        // TADA... you got the list!!
                        //
                        // ************************************************************

                        // CHECK IF TIME IS BETWEEN 8:00 PM AND 8:00 AM ...
                        DateTime today = DateTime.Now;

                        TimeSpan start = new TimeSpan(0, 0, 1); //12 Oclock
                        TimeSpan end = new TimeSpan(8, 0, 0); //8 Oclock


                        DateTime fromEight = today.Date.AddHours(20);
                        DateTime toEight = today.Date.AddHours(32);

                        //if time is between night 12 to morning 8, subtract one day 
                        if (today.TimeOfDay > start && today.TimeOfDay < end)
                        {
                            fromEight = today.AddDays(-1).Date.AddHours(20);
                            toEight = today.AddDays(-1).Date.AddHours(32);
                        }

                        if (today >= fromEight && today <= toEight)
                        {
                            // YES!! IT'S NIGHT TIME...
                            var vRelLvl = _context.GpReleaseStrategyLevels
                                .Where(r => r.ReleaseCode == rAuth.ReleaseCode)
                                .Select(r => r.GpReleaseStrategy)
                                .ToArray();

                            var vRelStr = _context.GpReleaseStrategy
                                .Where(r => vRelLvl.Contains(r.GpReleaseStrategy))
                                .ToList();

                            var vEmp = new List<string>();

                            foreach (
                                    var vEmptmp in vRelStr.Select(relObj => _context.Employees
                                    .Where(e =>
                                            e.CompCode == relObj.CompCode &&
                                            e.WrkGrp == relObj.WrkGrp &&
                                            e.UnitCode == relObj.UnitCode &&
                                            e.DeptCode == relObj.DeptCode &&
                                            e.StatCode == relObj.StatCode)
                                    .Select(e => e.EmpUnqId)
                                    .ToList())
                                    )
                            {
                                vEmp.AddRange(vEmptmp);
                            }

                            var vGp = _context.GatePass
                                .Where(g => g.ReleaseStatusCode == "I" &&
                                            vEmp.Contains(g.EmpUnqId)
                                )
                                .Select(g => g.Id)
                                .ToArray();


                            app = _context.ApplReleaseStatus
                                .Where(l =>
                                    l.ReleaseGroupCode == "GP" &&
                                    l.ReleaseStatusCode == "I" &&
                                    vGp.Contains(l.ApplicationId))
                                .ToList();
                        }
                    }
                    else
                    {
                        app = _context.ApplReleaseStatus
                            .Where(l => rAuth.ReleaseCode == l.ReleaseCode && l.ReleaseStatusCode == ReleaseStatus.InRelease)
                            .ToList();
                    }

                    var appIds = app.Select(a => a.ApplicationId).ToArray();

                    var gp = _context.GatePass
                        .Include(r => r.ReleaseGroup)
                        .Include(rs => rs.RelStrategy)
                        .Where(l => appIds.Contains(l.Id))
                        .Select(Mapper.Map<GatePass, GatePassDto>)
                        .ToList();


                    foreach (GatePassDto dto in gp)
                    {
                        var appl = _context.ApplReleaseStatus
                            .Where(l =>
                                l.YearMonth == dto.YearMonth &&
                                l.ReleaseGroupCode == dto.ReleaseGroupCode &&
                                l.ApplicationId == dto.Id)
                            .ToList()
                            .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

                        foreach (ApplReleaseStatusDto applReleaseStatusDto in appl)
                        {
                            Employees vEmp = _context.Employees
                                .FirstOrDefault(e => e.EmpUnqId == applReleaseStatusDto.ReleaseStrategy);

                            GpReleaseStrategies vRelStr = _context.GpReleaseStrategy
                                .FirstOrDefault(e => e.CompCode == vEmp.CompCode &&
                                                     e.WrkGrp == vEmp.WrkGrp &&
                                                     e.UnitCode == vEmp.UnitCode &&
                                                     e.DeptCode == vEmp.DeptCode &&
                                                     e.StatCode == vEmp.StatCode);
                            if (vRelStr ==null) continue;

                            var vRelStrLvl = _context.GpReleaseStrategyLevels
                                .Where(g => g.GpReleaseStrategy == vRelStr.GpReleaseStrategy);

                            var vRelCodes = vRelStrLvl.Select(g => g.ReleaseCode);

                            var relCode = _context.ReleaseAuth
                                .Where(r => vRelCodes.Contains(r.ReleaseCode))
                                .ToList();

                            var relCodeDay = _context.ReleaseAuth
                                .Where(r => r.ReleaseCode == applReleaseStatusDto.ReleaseCode)
                                .ToList();

                            relCode.AddRange(relCodeDay);

                            foreach (ReleaseAuth auth in relCode.Where(auth => auth.EmpUnqId == empUnqId))
                            {
                                applReleaseStatusDto.ReleaseAuth = empUnqId;
                                applReleaseStatusDto.ReleaseStrategy = vRelStr.GpReleaseStrategy;
                                applReleaseStatusDto.ReleaseCode = auth.ReleaseCode;
                                    
                                dto.ApplReleaseStatus = new List<ApplReleaseStatusDto>
                                {
                                    applReleaseStatusDto
                                };
                            }
                        }


                        EmployeeDto employeeDto = _context.Employees
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
                                DesgName = e.Designations.DesgName,

                                Location = e.Location
                            })
                            .Single(e => e.EmpUnqId == dto.EmpUnqId);

                        dto.DeptName = employeeDto.DeptName;
                        dto.StatName = employeeDto.StatName;
                        dto.EmpName = employeeDto.EmpName;
                        dto.ModeName = dto.GetMode(dto.Mode);
                        dto.StatusName = dto.GetStatus(dto.GatePassStatus);
                        dto.BarCode = dto.GetBarcode(dto.EmpUnqId, dto.Id);

                        //dto.Employee = employeeDto;
                    } //foreach dto in gp

                    // Add this to output list and loop if more...
                    outputGp.AddRange(gp);
                } // End for loop for release auth

                return Ok(outputGp);
            }
            else if (releaseGroupCode == ReleaseGroups.GatePassAdvice)
            {
                //In case of gate pass advice

                // find release authorization for given employee
                var relAuth = _context.ReleaseAuth
                    .Where(r => r.EmpUnqId == empUnqId)
                    .ToList();

                var outputGp = new List<GpAdviceDto>();

                //for each release auth
                foreach (ReleaseAuth rAuth in relAuth)
                {
                    // get list of all application release status
                    // which are in release

                    var app = _context.ApplReleaseStatus
                        .Where(l => 
                            rAuth.ReleaseCode == l.ReleaseCode && 
                            l.ReleaseStatusCode == "I" && 
                            l.ReleaseGroupCode == ReleaseGroups.GatePassAdvice
                        )
                        .ToList();

                    var appIds = app.Select(a => a.ApplicationId).ToArray();

                    // find the gate pass advice related to above list of app release 

                    var gp = _context.GpAdvices
                        .Include(r => r.ReleaseGroup)
                        .Include(rs => rs.RelStrategy)
                        .Where(l => appIds.Contains(l.GpAdviceNo))
                        .ToList()
                        .Select(Mapper.Map<GpAdvices, GpAdviceDto>).ToList();

                    // now for all gate pass advices,
                    foreach (GpAdviceDto dto in gp)
                    {
                        //get relevant app release object 
                        var appl = app
                            .Where(l =>
                                l.YearMonth == dto.YearMonth &&
                                l.ReleaseGroupCode == dto.ReleaseGroupCode &&
                                l.ApplicationId == dto.GpAdviceNo && 
                                l.ReleaseCode == rAuth.ReleaseCode
                                )
                            .ToList()
                            .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

                        //for each app release lines
                        foreach (ApplReleaseStatusDto applReleaseStatusDto in appl)
                        {
                            var relCode = _context.ReleaseAuth
                                .Where(r => r.ReleaseCode == applReleaseStatusDto.ReleaseCode)
                                .ToList();

                            foreach (ReleaseAuth unused in relCode.Where(auth => auth.EmpUnqId == empUnqId))
                            {
                                applReleaseStatusDto.ReleaseAuth = empUnqId;
                                dto.ApplReleaseStatus = new List<ApplReleaseStatusDto>
                                {
                                    applReleaseStatusDto
                                };
                            }
                        }


                        EmployeeDto employeeDto = _context.Employees
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
                                DesgName = e.Designations.DesgName,

                                Location = e.Location
                            })
                            .Single(e => e.EmpUnqId == dto.EmpUnqId);

                        dto.DeptName = employeeDto.DeptName;
                        dto.StatName = employeeDto.StatName;
                        dto.EmpName = employeeDto.EmpName;

                        //dto.Employee = employeeDto;
                    } //foreach dto in gp

                    // Add this to output list and loop if more...
                    outputGp.AddRange(gp);
                } // End for loop for release auth

                return Ok(outputGp);
            }
            else if (releaseGroupCode == ReleaseGroups.ShiftSchedule)
            {
                /////////////////////////////////////////////

                // find release authorization for given employee
                var relAuth = _context.ReleaseAuth
                    .Where(r => r.EmpUnqId == empUnqId)
                    .ToList();

                // create return data object
                var resultData = new List<ScheduleData>();

                //for each release auth
                foreach (ReleaseAuth rAuth in relAuth)
                {
                    // get list of all application release status
                    // which are in release

                    var app = _context.ApplReleaseStatus
                        .Where(l => 
                            rAuth.ReleaseCode == l.ReleaseCode && 
                            l.ReleaseStatusCode == "I" && 
                            l.ReleaseGroupCode == ReleaseGroups.ShiftSchedule
                        )
                        .ToList();

                    var appIds = app.Select(a => a.ApplicationId).ToArray();

                    // find the gate pass advice related to above list of app release 

                    var sch = _context.ShiftSchedules
                        .Include(r => r.ReleaseGroup)
                        .Include(rs => rs.RelStrategy)
                        .Where(s => appIds.Contains(s.ScheduleId))
                        .ToList()
                        .Select(Mapper.Map<ShiftSchedules, ShiftScheduleDto>).ToList();

                    // now for all gate pass advices,

                    int prevId = 0;

                    foreach (ShiftScheduleDto dto in sch)
                    {
                        dto.ShiftScheduleDetails = new List<ShiftScheduleDetailDto>();

                        var schDtl = _context.ShiftScheduleDetails
                            .Where(s => s.YearMonth == dto.YearMonth &&
                                        s.ScheduleId == dto.ScheduleId &&
                                        s.EmpUnqId == dto.EmpUnqId)
                            .ToList();

                        dto.ShiftScheduleDetails.AddRange(Mapper.Map<List<ShiftScheduleDetails>, List<ShiftScheduleDetailDto>>(schDtl));

                        //get relevant app release object 
                        var appl = app
                            .Where(l =>
                                l.YearMonth == dto.YearMonth &&
                                l.ReleaseGroupCode == dto.ReleaseGroupCode &&
                                l.ApplicationId == dto.ScheduleId && 
                                l.ReleaseCode == rAuth.ReleaseCode
                                )
                            .ToList()
                            .Select(Mapper.Map<ApplReleaseStatus, ApplReleaseStatusDto>);

                        //for each app release lines
                        foreach (ApplReleaseStatusDto applReleaseStatusDto in appl)
                        {
                            var relCode = _context.ReleaseAuth
                                .Where(r => r.ReleaseCode == applReleaseStatusDto.ReleaseCode)
                                .ToList();

                            foreach (ReleaseAuth unused in relCode.Where(auth => auth.EmpUnqId == empUnqId))
                            {
                                applReleaseStatusDto.ReleaseAuth = empUnqId;
                                dto.ApplReleaseStatus = new List<ApplReleaseStatusDto>
                                {
                                    applReleaseStatusDto
                                };
                            }
                        }


                        EmployeeDto employeeDto = _context.Employees
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
                                DesgName = e.Designations.DesgName,

                                Location = e.Location
                            })
                            .Single(e => e.EmpUnqId == dto.EmpUnqId);

                        dto.DeptName = employeeDto.DeptName;
                        dto.StatName = employeeDto.StatName;
                        dto.EmpName = employeeDto.EmpName;


                        if (dto.ScheduleId != prevId)
                        {
                            // add current object to return data and create new one
                            var thisData = new ScheduleData
                            {
                                YearMonth = dto.YearMonth,
                                ScheduleId = dto.ScheduleId,
                                AddUser = dto.AddUser,
                                Schedules = new List<ShiftScheduleDto>()
                            };

                            thisData.Schedules.Add(dto);
                            resultData.Add(thisData);

                            prevId = dto.ScheduleId;
                        }
                        else
                        {
                            resultData.FirstOrDefault(r =>
                                r.YearMonth == dto.YearMonth && r.ScheduleId == dto.ScheduleId)
                                ?.Schedules.Add(dto);
                        }

                    } //foreach dto in schedules

                } // End for loop for release auth

                //string json = JsonConvert.SerializeObject(resultData);
                return Ok(resultData);
            } // App release For Schedules...
            else
                return BadRequest("Not implemented");
        }

        [HttpPost]
        [ActionName("UpdateApplReleaseStatus")]
        public IHttpActionResult UpdateApplReleaseStatus([FromBody] object requestData, string empUnqId,
            string releaseStatusCode)
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
                    applicationDetail.ReleaseDate = today;
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

        [HttpPost]
        [ActionName("updategpstatus")]
        public IHttpActionResult UpdateGpStatus([FromBody] object requestData, string empUnqId,
            string releaseStatusCode, string releaseGroupCode)
        {
            if (releaseGroupCode == ReleaseGroups.GatePass)
            {
                try
                {
                    var gp = GatePassRelease(requestData, empUnqId, releaseStatusCode);
                    return Ok(gp);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.ToString());
                }
            }
            else if (releaseGroupCode == ReleaseGroups.GatePassAdvice)
            {
                try
                {
                    var gp = GatePassAdviceRelease(requestData, empUnqId, releaseStatusCode);
                    return Ok(gp);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.ToString());
                }
            }
            else if (releaseGroupCode == ReleaseGroups.ShiftSchedule)
            {
                try
                {
                    var sch = ShiftScheduleRelease(requestData, empUnqId, releaseStatusCode);
                    return Ok(sch);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.ToString());
                }
            }
            else
            {
                return BadRequest("Not implemented.");
            }
        }

        private GatePass GatePassRelease(object requestData, string empUnqId, string releaseStatusCode)
        {
            //Following details will be filled:
            //YearMonth, ReleaseGroupCode, ApplicationId, ReleaseStrategy, ReleaseStrategyLevel, ReleaseCode

            string vRelStr;
            ApplReleaseStatus dto = JsonConvert.DeserializeObject<ApplReleaseStatus>(requestData.ToString());


            ApplReleaseStatus applicationDetail = _context.ApplReleaseStatus
                .SingleOrDefault(
                    a => a.YearMonth == dto.YearMonth &&
                         a.ReleaseGroupCode == dto.ReleaseGroupCode &&
                         a.ApplicationId == dto.ApplicationId &&
                         a.ReleaseStrategyLevel == dto.ReleaseStrategyLevel
                );

            if (applicationDetail == null)
                throw new Exception("Invalid app release status detals...");

            applicationDetail.Remarks = dto.Remarks;

            //If releaseStatusCode is not I, we've nothing to do...
            if (applicationDetail.ReleaseStatusCode != ReleaseStatus.InRelease)
                throw new Exception("Application is not in release state.");


            /* ***********************************************************************
             * 
             *      CHECK IF TIME IS BETWEEN 8:00 PM AND 8:00 AM ...
             * 
             *********************************************************************** */
            //first verfy if release code is correct based on the relase code
            DateTime today = DateTime.Now;

//          DateTime fromEight = DateTime.Today.AddHours(20);
//          DateTime toEight = DateTime.Today.AddHours(28);
//          TimeSpan fromEight = new TimeSpan(20,00,00);
//          TimeSpan toEight = new TimeSpan(07,59,59);
//          DateTime today = DateTime.Now;

            TimeSpan start = new TimeSpan(0, 0, 1); //12 Oclock
            TimeSpan end = new TimeSpan(8, 0, 0); //8 Oclock
            
            DateTime fromEight = today.Date.AddHours(20);
            DateTime toEight = today.Date.AddHours(32);

            //if time is between night 12 to morning 8, subtract one day 
            if (today.TimeOfDay > start && today.TimeOfDay < end)
            {
                fromEight = today.AddDays(-1).Date.AddHours(20);
                toEight = today.AddDays(-1).Date.AddHours(32);
            }
            

            ReleaseAuth relAuth;

            if (today >= fromEight && today <= toEight)
            {
                // THIS IS NIGHT DUTY RELEASE TIME...

                //Get the release strategy
                var vEmp = _context.Employees
                    .FirstOrDefault(e => e.EmpUnqId == applicationDetail.ReleaseStrategy);

                vRelStr = _context.GpReleaseStrategy
                    .Where(
                        g =>
                            g.CompCode == vEmp.CompCode &&
                            g.WrkGrp == vEmp.WrkGrp &&
                            g.UnitCode == vEmp.UnitCode &&
                            g.DeptCode == vEmp.DeptCode &&
                            g.StatCode == vEmp.StatCode &&
                            g.NightFlag
                    )
                    .Select(g => g.GpReleaseStrategy)
                    .FirstOrDefault();

                var vRelStrlev = _context.GpReleaseStrategyLevels
                    .Where(g => g.GpReleaseStrategy == vRelStr)
                    .Select(g => g.ReleaseCode)
                    .ToArray();

                relAuth = _context.ReleaseAuth
                    .FirstOrDefault(
                        r =>
                            vRelStrlev.Contains(r.ReleaseCode) &&
                            r.EmpUnqId == empUnqId &&
                            r.Active &&
                            r.IsGpNightReleaser &&
                            today >= r.ValidFrom &&
                            today <= r.ValidTo
                    );

                if (relAuth == null)
                    throw new Exception("Employee not authorized for night release.");
                else
                    applicationDetail.ReleaseCode = relAuth.ReleaseCode;
            }
            else
            {
                relAuth = _context.ReleaseAuth
                    .Single(
                        r =>
                            r.ReleaseCode == applicationDetail.ReleaseCode &&
                            r.EmpUnqId == empUnqId &&
                            r.Active &&
                            today >= r.ValidFrom &&
                            today <= r.ValidTo
                    );

                if (relAuth == null)
                    throw new Exception("Invalid releaser code. Check Active, Valid From, Valid to.");

                vRelStr = applicationDetail.ReleaseStrategy;
            }


            //releaser is Ok. Now find release strategy level details
            var relStrLevel = _context.GpReleaseStrategyLevels
                .Single(
                    r =>
                        r.ReleaseGroupCode == applicationDetail.ReleaseGroupCode &&
                        r.GpReleaseStrategy == vRelStr &&
                        r.GpReleaseStrategyLevel == applicationDetail.ReleaseStrategyLevel &&
                        r.ReleaseCode == applicationDetail.ReleaseCode
                );

            if (relStrLevel == null)
                throw new Exception("Release strategy detail not found:");


            var gp = _context.GatePass
                .Single(
                    g =>
                        g.YearMonth == applicationDetail.YearMonth &&
                        g.Id == applicationDetail.ApplicationId
                );

            gp.GpRemarks = dto.Remarks;


            using (var transaction = _context.Database.BeginTransaction())
            {
                if (releaseStatusCode == ReleaseStatus.ReleaseRejected)
                {
                    //set this application details status to "R"
                    //we'll not set next level to "I", it'll forever be "N"
                    applicationDetail.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;

                    //now set leave application header release status to "R" as well
                    gp.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;
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
                            throw new Exception(
                                "This is not final release, and next release not found! Check database.");

                        //change status of next level object
                        nextApplicationLevel.ReleaseStatusCode = ReleaseStatus.InRelease;

                        //also update leave application status to "P"
                        gp.ReleaseStatusCode = ReleaseStatus.PartiallyReleased;
                    }
                    else
                    {
                        // DISABLED ON 04.07.2018
                        // NOW SINCE ESS IS LIVE, HR WILL REVIEW AND POST 
                        // FROM ESS TO ATTENDANCE.


                        ////if this IS final release, then set leave app header status to "F"
                        gp.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                    }

                    //Finally set this application details status to "F"
                    applicationDetail.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                    applicationDetail.ReleaseDate = today;
                    applicationDetail.ReleaseAuth = empUnqId;
                }


                //finally update database
                _context.SaveChanges();

                // UPDATE RELEASESTRATEGY MANUALLY, AS IT IS KEY FIELD AND CANNOT BE UPDATED BY ENTITY FRAMEWORK

                string strSql = "update ApplReleaseStatus set ReleaseStrategy = '" + vRelStr + "' " +
                                "where ReleaseGroupCode = 'GP' " +
                                "and ApplicationId = " + applicationDetail.ApplicationId + "";

                _context.Database.ExecuteSqlCommand(strSql);

                //now commit changes...
                transaction.Commit();
            }

            return gp;
        }

        private GpAdvices GatePassAdviceRelease(object requestData, string empUnqId, string releaseStatusCode)
        {
            ApplReleaseStatus dto = JsonConvert.DeserializeObject<ApplReleaseStatus>(requestData.ToString());


            ApplReleaseStatus applicationDetail = _context.ApplReleaseStatus
                .SingleOrDefault(
                    a => a.YearMonth == dto.YearMonth &&
                         a.ReleaseGroupCode == dto.ReleaseGroupCode &&
                         a.ApplicationId == dto.ApplicationId &&
                         a.ReleaseStrategyLevel == dto.ReleaseStrategyLevel
                );

            if (applicationDetail == null)
                throw new Exception("Invalid app release status detals...");

            applicationDetail.Remarks = dto.Remarks;

            //If releaseStatusCode is not I, we've nothing to do...
            if (applicationDetail.ReleaseStatusCode != ReleaseStatus.InRelease)
                throw new Exception("Application is not in release state.");


            ReleaseAuth relAuth = _context.ReleaseAuth
                .Single(
                    r =>
                        r.ReleaseCode == applicationDetail.ReleaseCode &&
                        r.EmpUnqId == empUnqId &&
                        r.Active
                );

            if (relAuth == null)
                throw new Exception("Invalid releaser code. Check Active, Valid From, Valid to.");

            var vRelStr = applicationDetail.ReleaseStrategy;


            //releaser is Ok. Now find release strategy level details
            var relStrLevel = _context.GaReleaseStrategyLevels
                .Single(
                    r =>
                        r.ReleaseGroupCode == applicationDetail.ReleaseGroupCode &&
                        r.GaReleaseStrategy == vRelStr &&
                        r.GaReleaseStrategyLevel == applicationDetail.ReleaseStrategyLevel &&
                        r.ReleaseCode == applicationDetail.ReleaseCode
                );

            if (relStrLevel == null)
                throw new Exception("Release strategy detail not found:");


            var gp = _context.GpAdvices
                .Single(
                    g =>
                        g.YearMonth == applicationDetail.YearMonth &&
                        g.GpAdviceNo == applicationDetail.ApplicationId
                );

            gp.Remarks = dto.Remarks;

            using (var transaction = _context.Database.BeginTransaction())
            {
                if (releaseStatusCode == ReleaseStatus.ReleaseRejected)
                {
                    //set this application details status to "R"
                    //we'll not set next level to "I", it'll forever be "N"
                    applicationDetail.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;

                    //now set leave application header release status to "R" as well
                    gp.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;
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
                            throw new Exception(
                                "This is not final release, and next release not found! Check database.");

                        //change status of next level object
                        nextApplicationLevel.ReleaseStatusCode = ReleaseStatus.InRelease;

                        //also update leave application status to "P"
                        gp.ReleaseStatusCode = ReleaseStatus.PartiallyReleased;
                    }
                    else
                    {
                        ////if this IS final release, then set leave app header status to "F"
                        gp.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                    }

                    //Finally set this application details status to "F"
                    applicationDetail.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                    applicationDetail.ReleaseDate = DateTime.Now.Date;
                    applicationDetail.ReleaseAuth = empUnqId;
                }


                //finally update database
                _context.SaveChanges();

                // UPDATE RELEASESTRATEGY MANUALLY, AS IT IS KEY FIELD AND CANNOT BE UPDATED BY ENTITY FRAMEWORK

                string strSql = "update ApplReleaseStatus set ReleaseStrategy = '" + vRelStr + "' " +
                                "where ReleaseGroupCode = 'GA' " +
                                "and ApplicationId = " + applicationDetail.ApplicationId + "";

                _context.Database.ExecuteSqlCommand(strSql);

                //now commit changes...
                transaction.Commit();
            }

            return gp;
        }

        private List<ShiftSchedules> ShiftScheduleRelease(object requestData, string empUnqId, string releaseStatusCode)
        {
            ApplReleaseStatus dto = JsonConvert.DeserializeObject<ApplReleaseStatus>(requestData.ToString());


            List<ShiftSchedules> resultData = new List<ShiftSchedules>();

            var allAppReleaseObj = _context.ApplReleaseStatus
                .Where(a => a.YearMonth == dto.YearMonth &&
                            a.ReleaseGroupCode == dto.ReleaseGroupCode &&
                            a.ApplicationId == dto.ApplicationId &&
                            a.ReleaseStrategyLevel == dto.ReleaseStrategyLevel )
                .ToList();

            foreach (ApplReleaseStatus appRelobj in allAppReleaseObj)
            {
                ApplReleaseStatus applicationDetail = _context.ApplReleaseStatus
                    .SingleOrDefault(
                        a => a.YearMonth == appRelobj.YearMonth &&
                             a.ReleaseGroupCode == appRelobj.ReleaseGroupCode &&
                             a.ApplicationId == appRelobj.ApplicationId &&
                             a.ReleaseStrategy == appRelobj.ReleaseStrategy &&
                             a.ReleaseStrategyLevel == appRelobj.ReleaseStrategyLevel
                    );

                if (applicationDetail == null)
                    throw new Exception("Invalid app release status detals...");

                applicationDetail.Remarks = appRelobj.Remarks;

                //If releaseStatusCode is not I, we've nothing to do...
                if (applicationDetail.ReleaseStatusCode != ReleaseStatus.InRelease)
                    throw new Exception("Application is not in release state.");


                ReleaseAuth relAuth = _context.ReleaseAuth
                    .Single(
                        r =>
                            r.ReleaseCode == applicationDetail.ReleaseCode &&
                            r.EmpUnqId == empUnqId &&
                            r.Active
                    );

                if (relAuth == null)
                    throw new Exception("Invalid releaser code. Check Active, Valid From, Valid to.");

                var vRelStr = applicationDetail.ReleaseStrategy;


                //releaser is Ok. Now find release strategy level details
                var relStrLevel = _context.ReleaseStrategyLevels
                    .Single(
                        r =>
                            r.ReleaseGroupCode == applicationDetail.ReleaseGroupCode &&
                            r.ReleaseStrategy == vRelStr &&
                            r.ReleaseStrategyLevel == applicationDetail.ReleaseStrategyLevel &&
                            r.ReleaseCode == applicationDetail.ReleaseCode
                    );

                if (relStrLevel == null)
                    throw new Exception("Release strategy detail not found:");


                var sch = _context.ShiftSchedules
                    .Single(
                        g =>
                            g.YearMonth == applicationDetail.YearMonth &&
                            g.ScheduleId == applicationDetail.ApplicationId &&
                            g.ReleaseStrategy == applicationDetail.ReleaseStrategy
                    );

                sch.Remarks = dto.Remarks;

                using (DbContextTransaction transaction = _context.Database.BeginTransaction())
                {
                    if (releaseStatusCode == ReleaseStatus.ReleaseRejected)
                    {
                        //set this application details status to "R"
                        //we'll not set next level to "I", it'll forever be "N"
                        applicationDetail.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;

                        //now set leave application header release status to "R" as well
                        sch.ReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                    }
                    else if (releaseStatusCode == ReleaseStatus.FullyReleased)
                    {
                        //if this is NOT the final release, set next level release status to "I"
                        if (!applicationDetail.IsFinalRelease)
                        {
                            //get next application level object
                            ApplReleaseStatus nextApplicationLevel = _context.ApplReleaseStatus
                                .Single(
                                    a =>
                                        a.YearMonth == applicationDetail.YearMonth &&
                                        a.ReleaseGroupCode == applicationDetail.ReleaseGroupCode &&
                                        a.ApplicationId == applicationDetail.ApplicationId &&
                                        a.ReleaseStrategy == applicationDetail.ReleaseStrategy &&
                                        a.ReleaseStrategyLevel == applicationDetail.ReleaseStrategyLevel + 1
                                );

                            if (nextApplicationLevel == null)
                                throw new Exception(
                                    "This is not final release, and next release not found! Check database.");

                            //change status of next level object
                            nextApplicationLevel.ReleaseStatusCode = ReleaseStatus.InRelease;

                            //also update leave application status to "P"
                            sch.ReleaseStatusCode = ReleaseStatus.PartiallyReleased;
                            sch.ReleaseUser = empUnqId;
                            sch.ReleaseDt = DateTime.Now;
                        }
                        else
                        {
                            ////if this IS final release, then set leave app header status to "F"
                            sch.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                            sch.ReleaseUser = empUnqId;
                            sch.ReleaseDt = DateTime.Now;
                        }

                        //Finally set this application details status to "F"
                        applicationDetail.ReleaseStatusCode = ReleaseStatus.FullyReleased;
                        applicationDetail.ReleaseDate = DateTime.Now.Date;
                        applicationDetail.ReleaseAuth = empUnqId;
                    }


                    //finally update database
                    _context.SaveChanges();

                    // UPDATE RELEASESTRATEGY MANUALLY, AS IT IS KEY FIELD AND CANNOT BE UPDATED BY ENTITY FRAMEWORK

                    string strSql = "update ApplReleaseStatus set ReleaseStrategy = '" + vRelStr + "' " +
                                    "where ReleaseGroupCode = 'SS' " +
                                    "and ApplicationId = " + applicationDetail.ApplicationId + " " +
                                    "and ReleaseStrategy = '" + applicationDetail.ReleaseStrategy + "' " ;

                    _context.Database.ExecuteSqlCommand(strSql);

                    //now commit changes...
                    transaction.Commit();

                    resultData.Add(sch);
                }
            }
            
            return resultData;
        }

    }
}