using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using AutoMapper;
using ESS.Dto;
using ESS.Models;

namespace ESS.App_Start
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Employees, EmployeeDto>();
            CreateMap<EmployeeDto, Employees>();

            CreateMap<Company, CompanyDto>();
            CreateMap<WorkGroups, WorkGroupDto>();
            CreateMap<Units, UnitDto>();
            CreateMap<Departments, DepartmentDto>();
            CreateMap<Stations, StationDto>();
            CreateMap<Sections, SectionDto>();
            CreateMap<Categories, CategoryDto>();
            CreateMap<EmpTypes, EmpTypeDto>();
            CreateMap<Grades, GradeDto>();
            CreateMap<Designations, DesignationDto>();
            CreateMap<ReleaseGroups, ReleaseGroupDto>();
            CreateMap<ReleaseStrategies, ReleaseStrategyDto>();
            CreateMap<ReleaseStrategyLevels, ReleaseStrategyLevelDto>();

            CreateMap<LeaveTypes, LeaveTypeDto>();

            CreateMap<LeaveBalance, LeaveBalanceDto>();
            CreateMap<LeaveBalanceDto, LeaveBalance>();

            CreateMap<LeaveApplications, LeaveApplicationDto>();
            CreateMap<LeaveApplicationDto, LeaveApplications>();

            CreateMap<LeaveApplicationDetails, LeaveApplicationDetailDto>();
            CreateMap<LeaveApplicationDetailDto, LeaveApplicationDetails>();

            CreateMap<ApplReleaseStatus, ApplReleaseStatusDto>();

            CreateMap<EmpUniform, EmpUniformDto>();

        }
    }
}