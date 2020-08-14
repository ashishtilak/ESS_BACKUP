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

            CreateMap<GatePass, GatePassDto>();

            CreateMap<EmpAddress, EmpAddressDto>();

            CreateMap<GpReleaseStrategies, GpReleaseStrategyDto>();
            CreateMap<GpReleaseStrategyLevels, GpReleaseStrategyLevelDto>();

            CreateMap<TaxDeclarations, TaxDeclarationDto>();
            CreateMap<TaxDeclarationDto, TaxDeclarations>();

            CreateMap<TaxDeclarationHistory, TaxDeclarationDto>();
            CreateMap<TaxDeclarationDto, TaxDeclarationHistory>();

            CreateMap<TaxDetailsInsurance, TaxDetailsInsuranceDto>();
            CreateMap<TaxDetailsInsuranceDto, TaxDetailsInsurance>();

            CreateMap<TaxDetailsMutualFunds, TaxDetailsMutualFundsDto>();
            CreateMap<TaxDetailsMutualFundsDto, TaxDetailsMutualFunds>();

            CreateMap<TaxDetailsNsc, TaxDetailsNscDto>();
            CreateMap<TaxDetailsNscDto, TaxDetailsNsc>();

            CreateMap<TaxDetailsPpf, TaxDetailsPpfDto>();
            CreateMap<TaxDetailsPpfDto, TaxDetailsPpf>();

            CreateMap<Banks, BankDto>();

            CreateMap<TaxDetailsBankDeposit, TaxDetailsBankDepositDto>();
            CreateMap<TaxDetailsBankDepositDto, TaxDetailsBankDeposit>();

            CreateMap<TaxDetailsUlip, TaxDetailsUlipDto>();
            CreateMap<TaxDetailsUlipDto, TaxDetailsUlip>();

            CreateMap<TaxDetailsSukanya, TaxDetailsSukanyaDto>();
            CreateMap<TaxDetailsSukanyaDto, TaxDetailsSukanya>();

            CreateMap<TaxDetailsRent, TaxDetailsRentDto>();
            CreateMap<TaxDetailsRentDto, TaxDetailsRent>();

            CreateMap<RoleAuth, RoleAuthDto>();

            CreateMap<GpAdvices, GpAdviceDto>();
            CreateMap<GpAdviceDto, GpAdvices>();
            CreateMap<GpAdviceDetails, GpAdviceDetailsDto>();
            CreateMap<GpAdviceDetailsDto, GpAdviceDetails>();
            CreateMap<GaReleaseStrategies, GaReleaseStrategyDto>();
            CreateMap<GaReleaseStrategyDto, GaReleaseStrategies>();
            CreateMap<GaReleaseStrategyLevels, GaReleaseStrategyLevelDto>();
            CreateMap<GaReleaseStrategyLevelDto, GaReleaseStrategyLevels>();

            CreateMap<Materials, MaterialDto>();
            CreateMap<MaterialDto, Materials>();

            CreateMap<Vendors, VendorDto>();
            CreateMap<VendorDto, Vendors>();

            CreateMap<Shifts, ShiftDto>();
            CreateMap<ShiftDto, Shifts>();

            CreateMap<ShiftSchedules, ShiftScheduleDto>();
            CreateMap<ShiftScheduleDto, ShiftSchedules>();
            CreateMap<ShiftScheduleDetails, ShiftScheduleDetailDto>();
            CreateMap<ShiftScheduleDetailDto, ShiftScheduleDetails>();
        }
    }
}