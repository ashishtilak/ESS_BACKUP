using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Migrations;
using ESS.Models;
using Newtonsoft.Json;
using TpaSanction = ESS.Models.TpaSanction;

namespace ESS.Controllers.Api
{
    public class TpaController : ApiController
    {
        private readonly ApplicationDbContext _context;

        public TpaController()
        {
            _context = new ApplicationDbContext();
        }


        // Get TPA for given date range for employees under specified releaser
        [HttpGet, ActionName("getpretpa")]
        public IHttpActionResult GetTpa(DateTime fromDate, DateTime toDate, string empUnqId)
        {
            var relAuth = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .ToList();

            relAuth.RemoveAll(r => r.ReleaseCode.StartsWith("GP_"));

            IEnumerable<string> relCodes = relAuth.Select(r => r.ReleaseCode).ToList();
            var relStrLvl = _context.ReleaseStrategyLevels
                .Where(r =>
                    r.ReleaseGroupCode == ReleaseGroups.Tpa &&
                    relCodes.Contains(r.ReleaseCode))
                .ToList();

            if (!relStrLvl.Any())
                return BadRequest("No records found.");

            var emps = relStrLvl.Select(r => r.ReleaseStrategy).ToList();


            var alreadyTpaEntry = _context.TpaSanctions.Where(
                    t => emps.Contains(t.EmpUnqId) &&
                         t.TpaDate >= fromDate && t.TpaDate <= toDate &&
                         t.PreReleaseStatusCode != ReleaseStatus.ReleaseRejected)
                .Select(t => t.EmpUnqId)
                .ToArray();

            emps.RemoveAll(e => alreadyTpaEntry.Contains(e));

            var result = new List<TpaSanctionDto>();

            foreach (string emp in emps)
            {
                for (DateTime dt = fromDate; dt <= toDate;)
                {
                    var newTpa = new TpaSanctionDto
                    {
                        Id = 0,
                        EmpUnqId = emp,
                        TpaDate = dt,
                        RequiredHours = 0,
                        ReleaseGroupCode = ReleaseGroups.Tpa,
                        PreReleaseStatusCode = ReleaseStatus.InRelease,
                        PreRemarks = "",
                        ReleaseStrategy = emp,
                    };

                    var yearMonth = int.Parse(dt.Year.ToString("0000") + dt.Month.ToString("00"));
                    newTpa.TpaShiftCode = _context.ShiftScheduleDetails
                        .FirstOrDefault(e => e.YearMonth == yearMonth
                                             && e.ShiftDay == dt.Day && e.EmpUnqId == emp)
                        ?.ShiftCode;

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
                        .Single(e => e.EmpUnqId == emp);

                    newTpa.EmpName = employeeDto.EmpName;
                    newTpa.CatName = employeeDto.CatName;
                    newTpa.DeptName = employeeDto.DeptName;
                    newTpa.StatName = employeeDto.StatName;
                    newTpa.GradeName = employeeDto.GradeName;
                    newTpa.DesgName = employeeDto.DesgName;

                    result.Add(newTpa);

                    dt = dt.AddDays(1);
                }
            }

            return Ok(result);
        }

        [HttpPost, ActionName("pretparequest")]
        public IHttpActionResult PreTpa(string empUnqId, [FromBody] object requestData)
        {
            var errors = new List<string>();

            try
            {
                var tpaSanctionDto = JsonConvert.DeserializeObject<List<TpaSanctionDto>>(requestData.ToString());

                using (DbContextTransaction transaction = _context.Database.BeginTransaction())
                {
                    foreach (TpaSanctionDto dto in tpaSanctionDto)
                    {
                        var tpaSanction = new TpaSanction
                        {
                            Id = 0,
                            EmpUnqId = dto.EmpUnqId,
                            TpaDate = dto.TpaDate,
                            TpaShiftCode = dto.TpaShiftCode,
                            RequiredHours = dto.RequiredHours,
                            PreJustification = dto.PreJustification,
                            ReleaseGroupCode = ReleaseGroups.Tpa,
                            ReleaseStrategy = dto.ReleaseStrategy,
                            PreReleaseStatusCode = ReleaseStatus.InRelease,
                            PreRemarks = dto.PreRemarks,
                            AddDt = DateTime.Now,
                            AddUser = dto.AddUser
                        };

                        _context.TpaSanctions.Add(tpaSanction);
                        _context.SaveChanges();

                        //get release info and store in tparelease
                        var relStrLvl = _context.ReleaseStrategyLevels
                            .Where(r => r.ReleaseGroupCode == ReleaseGroups.Tpa &&
                                        r.ReleaseStrategy == dto.EmpUnqId)
                            .ToList();

                        foreach (ReleaseStrategyLevels level in relStrLvl)
                        {
                            ReleaseAuth relAuth = _context.ReleaseAuth
                                .FirstOrDefault(ra => ra.ReleaseCode == level.ReleaseCode);

                            if (relAuth == null)
                            {
                                errors.Add("Release auth not found for emp: " + dto.EmpUnqId);
                                continue;
                            }

                            var tpaRelease = new TpaRelease
                            {
                                Id = 0,
                                ReleaseGroupCode = dto.ReleaseGroupCode,
                                TpaSanctionId = tpaSanction.Id,
                                ReleaseStrategy = level.ReleaseStrategy,
                                ReleaseStrategyLevel = level.ReleaseStrategyLevel,
                                ReleaseCode = level.ReleaseCode,
                                PreReleaseStatusCode =
                                    level.ReleaseStrategyLevel == 1
                                        ? ReleaseStatus.InRelease
                                        : ReleaseStatus.NotReleased,
                                PreReleaseDate = null,
                                PreReleaseAuth = relAuth.EmpUnqId,
                                PostReleaseAuth = relAuth.EmpUnqId,
                                IsFinalRelease = level.IsFinalRelease
                            };

                            _context.TpaReleases.Add(tpaRelease);
                            _context.SaveChanges();
                        }
                    }

                    _context.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                errors.Add("Error: " + ex);
            }

            if (errors.Count > 0)
                return Content(HttpStatusCode.BadRequest, errors);

            return Ok();
        }

        [HttpGet, ActionName("getprerequestedlist")]
        public IHttpActionResult GetPreRelease(string empUnqId)
        {
            string[] relAuth = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .Select(r => r.ReleaseCode)
                .ToArray();


            List<TpaReleaseDto> tpaReleaseDtos = _context.TpaReleases
                .Where(l =>
                    relAuth.Contains(l.ReleaseCode) &&
                    l.PreReleaseStatusCode == ReleaseStatus.InRelease
                ).AsEnumerable()
                .Select(Mapper.Map<TpaRelease, TpaReleaseDto>)
                .ToList();

            int[] ids = tpaReleaseDtos.Select(a => a.TpaSanctionId).ToArray();


            List<TpaSanctionDto> tpaSanctionList = _context.TpaSanctions
                .Include(e => e.Employee)
                .Include(r => r.ReleaseGroup)
                .Include(rs => rs.RelStrategy)
                .Where(l => ids.Contains(l.Id)).ToList()
                .Select(Mapper.Map<TpaSanction, TpaSanctionDto>)
                .ToList();

            foreach (TpaSanctionDto dto in tpaSanctionList)
            {
                List<TpaReleaseDto> apps = tpaReleaseDtos.Where(t => t.TpaSanctionId == dto.Id).ToList();

                dto.TpaReleaseStatus = new List<TpaReleaseDto>();
                foreach (TpaReleaseDto app in apps)
                {
                    List<ReleaseAuth> relCode = _context.ReleaseAuth
                        .Where(r => r.ReleaseCode == app.ReleaseCode)
                        .ToList();

                    foreach (ReleaseAuth unused in relCode.Where(auth => auth.EmpUnqId == empUnqId))
                        app.PreReleaseAuth = empUnqId;

                    dto.TpaReleaseStatus.Add(app);
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

                dto.EmpName = employeeDto.EmpName;
                dto.CatName = employeeDto.CatName;
                dto.DeptName = employeeDto.DeptName;
                dto.StatName = employeeDto.StatName;
                dto.GradeName = employeeDto.GradeName;
                dto.DesgName = employeeDto.DesgName;
            }

            return Ok(tpaSanctionList);
        }

        [HttpPost, ActionName("preapproval")]
        public IHttpActionResult PreReleaseTpa([FromBody] object requestData)
        {
            try
            {
                var tpaRleaseDtos = JsonConvert.DeserializeObject<List<TpaReleaseDto>>(requestData.ToString());

                using (DbContextTransaction transaction = _context.Database.BeginTransaction())
                {
                    foreach (TpaReleaseDto dto in tpaRleaseDtos)
                    {
                        TpaRelease tpaRelease = _context.TpaReleases
                            .FirstOrDefault(t =>
                                t.TpaSanctionId == dto.TpaSanctionId &&
                                t.ReleaseStrategyLevel == dto.ReleaseStrategyLevel);

                        if (tpaRelease == null)
                            return BadRequest("Invalid release details.");

                        tpaRelease.PreRemarks = dto.PreRemarks;

                        if (tpaRelease.PreReleaseStatusCode != ReleaseStatus.InRelease)
                            return BadRequest("Application is not in release state.");


                        //first verfy if release code is correct based on the relase code
                        DateTime today = DateTime.Now;

                        ReleaseAuth relAuth = _context.ReleaseAuth
                            .Single(
                                r =>
                                    r.ReleaseCode == tpaRelease.ReleaseCode &&
                                    r.EmpUnqId == tpaRelease.PreReleaseAuth &&
                                    r.Active &&
                                    today >= r.ValidFrom &&
                                    today <= r.ValidTo
                            );

                        if (relAuth == null)
                            BadRequest("Invalid releaser code. Check Active, Valid From, Valid to.");


                        //releaser is Ok. Now find release strategy level details
                        ReleaseStrategyLevels relStrLevel = _context.ReleaseStrategyLevels
                            .Single(
                                r =>
                                    r.ReleaseGroupCode == tpaRelease.ReleaseGroupCode &&
                                    r.ReleaseStrategy == tpaRelease.ReleaseStrategy &&
                                    r.ReleaseStrategyLevel == tpaRelease.ReleaseStrategyLevel &&
                                    r.ReleaseCode == tpaRelease.ReleaseCode
                            );

                        if (relStrLevel == null)
                            return BadRequest("Release strategy detail not found:");

                        TpaSanction tpaSanction = _context.TpaSanctions
                            .FirstOrDefault(t => t.Id == tpaRelease.TpaSanctionId);

                        if (tpaSanction == null)
                            return BadRequest("Corresponding TPA sanction request is not found!");

                        tpaSanction.PreRemarks = dto.PreRemarks;


                        if (dto.PreReleaseStatusCode == ReleaseStatus.ReleaseRejected)
                        {
                            tpaRelease.PreReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                            tpaSanction.PreReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                        }
                        else if (dto.PreReleaseStatusCode == ReleaseStatus.FullyReleased)
                        {
                            if (!tpaRelease.IsFinalRelease)
                            {
                                TpaRelease nextRelease = _context.TpaReleases
                                    .FirstOrDefault(t =>
                                        t.TpaSanctionId == dto.TpaSanctionId &&
                                        t.ReleaseStrategyLevel == dto.ReleaseStrategyLevel + 1);
                                if (nextRelease == null)
                                    return BadRequest(
                                        "This is not final release, and next level not found! Chk app release");
                                nextRelease.PreReleaseStatusCode = ReleaseStatus.InRelease;
                                tpaSanction.PreReleaseStatusCode = ReleaseStatus.PartiallyReleased;
                            }
                            else
                            {
                                tpaSanction.PreReleaseStatusCode = ReleaseStatus.FullyReleased;
                            }

                            tpaRelease.PreReleaseStatusCode = ReleaseStatus.FullyReleased;
                            tpaRelease.PreReleaseDate = DateTime.Now;
                            tpaRelease.PreReleaseAuth = dto.PreReleaseAuth;
                        }

                        _context.SaveChanges();

                        //string strSql = "update TpaReleases set ReleaseStrategy = '" + tpaRelease.ReleaseStrategy + "' " +
                        //                "where ReleaseGroupCode = 'OT' " +
                        //                "and TpaSanctionId = " + tpaRelease.TpaSanctionId + "";
                        //_context.Database.ExecuteSqlCommand(strSql);
                    }

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                return BadRequest("Error: " + ex);
            }

            return Ok();
        }

        // POST SANCTION
        [HttpGet, ActionName("getposttpa")]
        public IHttpActionResult GetPostTpa(DateTime fromDate, DateTime toDate, string empUnqId)
        {
            // get all sanctioned employee records between date range
            var relAuth = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .ToList();

            relAuth.RemoveAll(r => r.ReleaseCode.StartsWith("GP_"));

            IEnumerable<string> relCodes = relAuth.Select(r => r.ReleaseCode).ToList();
            var relStrLvl = _context.ReleaseStrategyLevels
                .Where(r =>
                    r.ReleaseGroupCode == ReleaseGroups.Tpa &&
                    relCodes.Contains(r.ReleaseCode))
                .ToList();

            if (!relStrLvl.Any())
                return BadRequest("No records found.");

            List<string> emps = relStrLvl.Select(r => r.ReleaseStrategy).ToList();

            // get location from one of the employees
            // if it is null, default to "IPU" manually
            string loc = _context.Employees
                .Where(e => e.EmpUnqId == emps.FirstOrDefault())
                .Select(e => e.Location)
                .FirstOrDefault() ?? "IPU";

            // create a string for SQL query
            string employees = "'" + string.Join("','", emps) + "'";

            var tpaAttdDto = new List<TpaAttdDto>();

            tpaAttdDto.AddRange(Helpers.CustomHelper.GetTpa(fromDate, toDate, employees, loc));

            List<TpaSanctionDto> preApprovedTpa = _context.TpaSanctions
                .Where(t =>
                    emps.Contains(t.EmpUnqId) &&
                    t.TpaDate >= fromDate && t.TpaDate <= toDate
                )
                .AsEnumerable()
                .Select(Mapper.Map<TpaSanction, TpaSanctionDto>)
                .ToList();

            var result = new List<TpaSanctionDto>();

            foreach (TpaAttdDto attdDto in tpaAttdDto)
            {
                // find already pre approved record. IF not found, create blank one
                TpaSanctionDto preDto = preApprovedTpa.FirstOrDefault(e =>
                    e.EmpUnqId == attdDto.EmpUnqId && e.TpaDate == attdDto.AttdDate) ?? new TpaSanctionDto
                {
                    Id = 0,
                    EmpUnqId = attdDto.EmpUnqId,
                    TpaDate = attdDto.AttdDate,
                    TpaShiftCode = attdDto.ScheDuleShift,
                    ReleaseGroupCode = ReleaseGroups.Tpa,
                    ReleaseStrategy = attdDto.EmpUnqId,
                };


                if (preDto.Id != 0)
                {
                    if (preDto.PostReleaseStatusCode == ReleaseStatus.InRelease ||
                        preDto.PostReleaseStatusCode == ReleaseStatus.PartiallyReleased ||
                        preDto.PostReleaseStatusCode == ReleaseStatus.FullyReleased
                    )
                    {
                        continue;
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
                    .Single(e => e.EmpUnqId == preDto.EmpUnqId);


                preDto.EmpName = employeeDto.EmpName;
                preDto.CatName = employeeDto.CatName;
                preDto.DeptName = employeeDto.DeptName;
                preDto.StatName = employeeDto.StatName;
                preDto.GradeName = employeeDto.GradeName;
                preDto.DesgName = employeeDto.DesgName;


                preDto.WrkHours = attdDto.ConsWrkHrs;
                preDto.ActShiftCode = attdDto.ConsShift;
                preDto.CalcOverTime = attdDto.CalcOverTime;
                preDto.SanctionTpa = attdDto.CalcOverTime;
                preDto.ConsIn = attdDto.ConsIn;
                preDto.ConsOut = attdDto.ConsOut;
                preDto.ConsWrkHrs = attdDto.ConsWrkHrs;
                preDto.Status = attdDto.Status;
                preDto.HalfDay = attdDto.HalfDay;
                preDto.LeaveType = attdDto.LeaveType;
                preDto.LeaveHalf = attdDto.LeaveHalf;
                preDto.Earlycome = attdDto.Earlycome;
                preDto.EarlyGoing = attdDto.EarlyGoing;
                preDto.LateCome = attdDto.LateCome;

                result.Add(preDto);
            }

            return Ok(result);
        }

        [HttpPost, ActionName("sanctiontpa")]
        public IHttpActionResult PostTpa([FromBody] object requestData, string empUnqId)
        {
            var errors = new List<string>();

            try
            {
                var tpaSanctionDto = JsonConvert.DeserializeObject<List<TpaSanctionDto>>(requestData.ToString());
                using (DbContextTransaction transaction = _context.Database.BeginTransaction())
                {
                    foreach (TpaSanctionDto dto in tpaSanctionDto)
                    {
                        var tpaSanction = _context.TpaSanctions.FirstOrDefault(t =>
                            t.Id == dto.Id);

                        if (tpaSanction == null)
                        {
                            // create TpaSanction record 
                            tpaSanction = new TpaSanction
                            {
                                Id = 0,
                                EmpUnqId = dto.EmpUnqId,
                                TpaDate = dto.TpaDate,
                                TpaShiftCode = dto.TpaShiftCode,
                                RequiredHours = 0,
                                PreJustification = "",
                                ReleaseGroupCode = ReleaseGroups.Tpa,
                                ReleaseStrategy = dto.ReleaseStrategy,
                                PreReleaseStatusCode = ReleaseStatus.InRelease,
                                PreRemarks = "",
                                AddDt = DateTime.Now,
                                AddUser = empUnqId,
                                ActShiftCode = dto.ActShiftCode,
                                WrkHours = dto.WrkHours,
                                SanctionTpa = dto.SanctionTpa,
                                PostJustification = dto.PostJustification,
                                PostReleaseStatusCode = ReleaseStatus.InRelease,
                                PostRemarks = dto.PostRemarks
                            };

                            _context.TpaSanctions.Add(tpaSanction);
                            _context.SaveChanges();

                            //get release info and store in tparelease
                            var relStrLvl = _context.ReleaseStrategyLevels
                                .Where(r => r.ReleaseGroupCode == ReleaseGroups.Tpa &&
                                            r.ReleaseStrategy == dto.EmpUnqId)
                                .ToList();

                            foreach (ReleaseStrategyLevels level in relStrLvl)
                            {
                                ReleaseAuth relAuth = _context.ReleaseAuth
                                    .FirstOrDefault(ra => ra.ReleaseCode == level.ReleaseCode);

                                if (relAuth == null)
                                {
                                    errors.Add("Release auth not found for emp: " + dto.EmpUnqId);
                                    continue;
                                }

                                var tpaRelease = new TpaRelease
                                {
                                    Id = 0,
                                    ReleaseGroupCode = dto.ReleaseGroupCode,
                                    TpaSanctionId = tpaSanction.Id,
                                    ReleaseStrategy = level.ReleaseStrategy,
                                    ReleaseStrategyLevel = level.ReleaseStrategyLevel,
                                    ReleaseCode = level.ReleaseCode,
                                    PreReleaseStatusCode = ReleaseStatus.InRelease,
                                    PostReleaseStatusCode =
                                        level.ReleaseStrategyLevel == 1
                                            ? ReleaseStatus.InRelease
                                            : ReleaseStatus.NotReleased,
                                    PreReleaseDate = null,
                                    PreReleaseAuth = relAuth.EmpUnqId,
                                    PostReleaseAuth = relAuth.EmpUnqId,
                                    IsFinalRelease = level.IsFinalRelease
                                };

                                _context.TpaReleases.Add(tpaRelease);
                                _context.SaveChanges();
                            }
                        }
                        else
                        {
                            if (tpaSanction.PreReleaseStatusCode == ReleaseStatus.InRelease ||
                                tpaSanction.PreReleaseStatusCode == ReleaseStatus.PartiallyReleased)
                            {
                                errors.Add("Record is not released for employee " + dto.EmpUnqId + " for date " +
                                           dto.TpaDate.ToString("dd/MM/yyy"));
                                continue;
                            }

                            tpaSanction.ActShiftCode = dto.ActShiftCode;
                            tpaSanction.WrkHours = dto.WrkHours;
                            tpaSanction.SanctionTpa = dto.SanctionTpa;
                            tpaSanction.PostJustification = dto.PostJustification;
                            tpaSanction.PostReleaseStatusCode = ReleaseStatus.InRelease;
                            tpaSanction.PostRemarks = dto.PostRemarks;

                            var tpaReleases = _context.TpaReleases
                                .Where(t => t.TpaSanctionId == dto.Id)
                                .ToList();
                            foreach (TpaRelease tpaRelease in tpaReleases)
                            {
                                tpaRelease.PostReleaseAuth = tpaRelease.PreReleaseAuth;
                                tpaRelease.PostReleaseStatusCode = tpaRelease.ReleaseStrategyLevel == 1
                                    ? ReleaseStatus.InRelease
                                    : ReleaseStatus.NotReleased;
                            }
                        }

                        _context.SaveChanges();
                    }

                    if (errors.Count > 0)
                        return Content(HttpStatusCode.BadRequest, errors);

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                errors.Add("Error: " + ex);
            }

            if (errors.Count > 0)
                return Content(HttpStatusCode.BadRequest, errors);

            return Ok();
        }

        [HttpGet, ActionName("getsanctionlist")]
        public IHttpActionResult GetPostRelease(string empUnqId)
        {
            string[] relAuth = _context.ReleaseAuth
                .Where(r => r.EmpUnqId == empUnqId)
                .Select(r => r.ReleaseCode)
                .ToArray();


            List<TpaReleaseDto> tpaReleaseDtos = _context.TpaReleases
                .Where(l =>
                    relAuth.Contains(l.ReleaseCode) &&
                    l.PostReleaseStatusCode == ReleaseStatus.InRelease
                ).AsEnumerable()
                .Select(Mapper.Map<TpaRelease, TpaReleaseDto>)
                .ToList();

            int[] ids = tpaReleaseDtos.Select(a => a.TpaSanctionId).ToArray();


            List<TpaSanctionDto> tpaSanctionList = _context.TpaSanctions
                .Include(e => e.Employee)
                .Include(r => r.ReleaseGroup)
                .Include(rs => rs.RelStrategy)
                .Where(l => ids.Contains(l.Id)).ToList()
                .Select(Mapper.Map<TpaSanction, TpaSanctionDto>)
                .ToList();

            foreach (TpaSanctionDto dto in tpaSanctionList)
            {
                List<TpaReleaseDto> apps = tpaReleaseDtos.Where(t => t.TpaSanctionId == dto.Id).ToList();

                dto.TpaReleaseStatus = new List<TpaReleaseDto>();
                foreach (TpaReleaseDto app in apps)
                {
                    List<ReleaseAuth> relCode = _context.ReleaseAuth
                        .Where(r => r.ReleaseCode == app.ReleaseCode)
                        .ToList();

                    foreach (ReleaseAuth unused in relCode.Where(auth => auth.EmpUnqId == empUnqId))
                        app.PreReleaseAuth = empUnqId;

                    dto.TpaReleaseStatus.Add(app);
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

                dto.EmpName = employeeDto.EmpName;
                dto.CatName = employeeDto.CatName;
                dto.DeptName = employeeDto.DeptName;
                dto.StatName = employeeDto.StatName;
                dto.GradeName = employeeDto.GradeName;
                dto.DesgName = employeeDto.DesgName;
            }

            return Ok(tpaSanctionList);
        }

        [HttpPost, ActionName("postapproval")]
        public IHttpActionResult PostReleaseTpa([FromBody] object requestData)
        {
            try
            {
                var tpaRleaseDtos = JsonConvert.DeserializeObject<List<TpaReleaseDto>>(requestData.ToString());

                using (DbContextTransaction transaction = _context.Database.BeginTransaction())
                {
                    foreach (TpaReleaseDto dto in tpaRleaseDtos)
                    {
                        TpaRelease tpaRelease = _context.TpaReleases
                            .FirstOrDefault(t =>
                                t.TpaSanctionId == dto.TpaSanctionId &&
                                t.ReleaseStrategyLevel == dto.ReleaseStrategyLevel);

                        if (tpaRelease == null)
                            return BadRequest("Invalid release details.");

                        tpaRelease.PostRemarks = dto.PostRemarks;

                        if (tpaRelease.PostReleaseStatusCode != ReleaseStatus.InRelease)
                            return BadRequest("Application is not in release state.");


                        //first verfy if release code is correct based on the relase code
                        DateTime today = DateTime.Now;

                        ReleaseAuth relAuth = _context.ReleaseAuth
                            .Single(
                                r =>
                                    r.ReleaseCode == tpaRelease.ReleaseCode &&
                                    r.EmpUnqId == tpaRelease.PostReleaseAuth &&
                                    r.Active &&
                                    today >= r.ValidFrom &&
                                    today <= r.ValidTo
                            );
                        if (relAuth == null)
                            BadRequest("Invalid releaser code. Check Active, Valid From, Valid to.");

                        //releaser is Ok. Now find release strategy level details
                        ReleaseStrategyLevels relStrLevel = _context.ReleaseStrategyLevels
                            .Single(
                                r =>
                                    r.ReleaseGroupCode == tpaRelease.ReleaseGroupCode &&
                                    r.ReleaseStrategy == tpaRelease.ReleaseStrategy &&
                                    r.ReleaseStrategyLevel == tpaRelease.ReleaseStrategyLevel &&
                                    r.ReleaseCode == tpaRelease.ReleaseCode
                            );

                        if (relStrLevel == null)
                            return BadRequest("Release strategy detail not found:");

                        TpaSanction tpaSanction = _context.TpaSanctions
                            .FirstOrDefault(t => t.Id == tpaRelease.TpaSanctionId);

                        if (tpaSanction == null)
                            return BadRequest("Corresponding TPA sanction request is not found!");

                        tpaSanction.PostRemarks = dto.PostRemarks;

                        if (dto.PostReleaseStatusCode == ReleaseStatus.ReleaseRejected)
                        {
                            tpaRelease.PostReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                            tpaSanction.PostReleaseStatusCode = ReleaseStatus.ReleaseRejected;
                        }
                        else if (dto.PostReleaseStatusCode == ReleaseStatus.FullyReleased)
                        {
                            if (!tpaRelease.IsFinalRelease)
                            {
                                TpaRelease nextRelease = _context.TpaReleases
                                    .FirstOrDefault(t =>
                                        t.TpaSanctionId == dto.TpaSanctionId &&
                                        t.ReleaseStrategyLevel == dto.ReleaseStrategyLevel + 1);
                                if (nextRelease == null)
                                    return BadRequest(
                                        "This is not final release, and next level not found! Chk app release");

                                nextRelease.PostReleaseStatusCode = ReleaseStatus.InRelease;
                                tpaSanction.PostReleaseStatusCode = ReleaseStatus.PartiallyReleased;
                            }
                            else
                            {
                                tpaSanction.PostReleaseStatusCode = ReleaseStatus.FullyReleased;
                            }

                            tpaRelease.PostReleaseStatusCode = ReleaseStatus.FullyReleased;
                            tpaRelease.PostReleaseDate = DateTime.Now;
                            tpaRelease.PostReleaseAuth = dto.PostReleaseAuth;
                        }

                        _context.SaveChanges();
                    }

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                return BadRequest("Error: " + ex);
            }

            return Ok();
        }
    }
}