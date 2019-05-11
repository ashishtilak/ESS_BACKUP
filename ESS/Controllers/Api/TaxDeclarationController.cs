using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.Entity;
using System.Reflection;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class TaxDeclarationController : ApiController
    {
        private readonly ApplicationDbContext _context;

        public TaxDeclarationController()
        {
            _context = new ApplicationDbContext();
        }

        /// <summary>
        /// Get's current config for Tax Declaration: year, actual flag, start date, end date
        /// </summary>
        /// <returns></returns>
        public IHttpActionResult GetTaxDeclarationConfig()
        {
            var config = _context.TaxConfig.FirstOrDefault();
            if (config == null) return BadRequest("Configuration not available.");

            return Ok(config);
        }

        /// <summary>
        /// Get's Tax Declaration details for an Employee
        /// </summary>
        /// <param name="empUnqId"> Employee unique Id</param>
        /// <param name="yearMonth"> Year Month</param>
        /// <param name="actualFlag"> Actual [1] or Provisional [0] </param>
        /// <returns></returns>
        public IHttpActionResult GetTaxDeclaration(string empUnqId, int yearMonth, bool actualFlag)
        {
            var taxDetails = _context.TaxDeclarations
                .Include(i => i.InsuranceDetails)
                .Include(m => m.MutualFundDetails)
                .Include(n => n.NscDetails)
                .Include(p => p.PpfDetails)
                .Include(b => b.BankDeposits)
                .Include(s => s.SukanyaDetails)
                .Include(u => u.UlipDetails)
                .Include(r => r.RentDetails)
                .Where(t => t.EmpUnqId == empUnqId && t.YearMonth == yearMonth && t.ActualFlag == actualFlag)
                .Select(Mapper.Map<TaxDeclarations, TaxDeclarationDto>)
                .ToList();

            return Ok(taxDetails);
        }

        /// <summary>
        /// Get tax declaration report for all employees. To be used for finance people.
        /// </summary>
        /// <param name="yearMonth"></param>
        /// <returns></returns>
        public IHttpActionResult GetTaxDeclarations(int yearMonth)
        {
            var taxDeclarations = _context.TaxDeclarations
                .Include(i => i.InsuranceDetails)
                .Include(m => m.MutualFundDetails)
                .Include(n => n.NscDetails)
                .Include(p => p.PpfDetails)
                .Include(b => b.BankDeposits)
                .Include(u => u.UlipDetails)
                .Include(s => s.SukanyaDetails)
                .Include(r => r.RentDetails)
                .Where(t => t.YearMonth == yearMonth)
                .Select(Mapper.Map<TaxDeclarations, TaxDeclarationDto>)
                .ToList();

            var employees = taxDeclarations.Select(e => e.EmpUnqId).Distinct().ToList();

            List<TaxDeclReportDto> report = new List<TaxDeclReportDto>();

            foreach (var emp in employees)
            {
                var empobj = _context.Employees
                    .FirstOrDefault(e => e.EmpUnqId == emp);

                if (empobj == null) return BadRequest("Employee not found : " + emp);

                var taxconfig = _context.TaxConfig.FirstOrDefault();
                if (taxconfig == null) return BadRequest("Tax config not found. ");

                var empRec = new TaxDeclReportDto();

                //get Provisional details:
                var prov = taxDeclarations.FirstOrDefault(e => e.EmpUnqId == emp && e.ActualFlag == false);
                if (prov != null)
                {
                    empRec.YearMonth = prov.YearMonth;
                    empRec.EmpUnqId = prov.EmpUnqId;
                    empRec.SapId = empobj.SapId;
                    empRec.EmpName = empobj.EmpName;
                    empRec.StartDt = taxconfig.StartDt;
                    empRec.EndDt = taxconfig.EndDt;

                    empRec.InsCode = 1;                                 //TODO: MAKE DYNAMIC - PROBABLY FROM A TABLE
                    empRec.InsPro = prov.TotalInsurancePremium;

                    empRec.UlipCode = 4;
                    empRec.UlipPro = prov.TotalUlip;

                    empRec.MfCode = 6;
                    empRec.MfPro = prov.TotalMutualFund;

                    empRec.PpfCode = 7;
                    empRec.PpfPro = prov.TotalPpfAmt;

                    empRec.NscCode = 11;
                    empRec.NscPro = prov.TotalNscAmount;

                    empRec.HomeLoanCode = 14;
                    empRec.HomeLoanPro = prov.HouseLoanPrincipal;

                    empRec.NotifiedMfCode = 18;
                    empRec.NotifiedMfPro = prov.TotalSukanya;

                    empRec.Child1Code = 24;
                    empRec.Child1Pro = prov.TuitionFeeChild1;

                    empRec.Child2Code = 25;
                    empRec.Child2Pro = prov.TuitionFeeChild2;

                    empRec.TermDepoCode = 26;
                    empRec.TermDepoPro = prov.TotalBankDepositAmount;

                    empRec.Total80C = empRec.InsPro + empRec.UlipPro + empRec.MfPro + empRec.PpfPro + empRec.NscPro +
                                      empRec.HomeLoanPro + empRec.NotifiedMfPro + empRec.Child1Pro + empRec.Child2Pro +
                                      empRec.TermDepoPro;


                    empRec.LongTermMf = 0;          //TODO: CONFIRM
                    empRec.MedicalPremium = prov.MedicalPremiumSelf + prov.MedicalPremiumParents;
                    empRec.EduLoanInterest = prov.EducationLoanInterest;
                    empRec.Nps = prov.NationalPensionScheme;
                    empRec.InterestOnLoan = prov.InterestOnLoan;
                    empRec.RentReceived = prov.RentalIncomePerMonth;
                    empRec.BankName = prov.LoanBank;
                    empRec.BankPan = prov.LoanBankPan;
                    empRec.AccomodationType = empobj.CompanyAcc ? "3" : "1";

                    empRec.RentPaidAprilPro = prov.RentDetails.Count > 0 ? prov.RentDetails[0].April : 0;
                    empRec.RentPaidPro = prov.TotalRentPaid;
                    empRec.LandLordName = prov.LandLordName;
                    empRec.LandLordPan = prov.LandLordPan;

                    empRec.RajivGandhiEquity = prov.RajivGandhiEquity;
                    empRec.MedicalPremiumSelf = prov.MedicalPremiumSelf;
                    empRec.MedicalPremiumParents = prov.MedicalPremiumParents;
                    empRec.MedicalPreventiveHealthCheckup = prov.MedicalPreventiveHealthCheckup;
                    empRec.PhysicalDisability = prov.PhysicalDisability;
                    empRec.SevereDisability = prov.SevereDisability;

                }


                //get Provisional details:
                var act = taxDeclarations.FirstOrDefault(e => e.EmpUnqId == emp && e.ActualFlag == true);
                if (act != null)
                {
                    empRec.YearMonth = act.YearMonth;
                    empRec.EmpUnqId = act.EmpUnqId;
                    empRec.SapId = empobj.SapId;
                    empRec.EmpName = empobj.EmpName;
                    empRec.StartDt = taxconfig.StartDt;
                    empRec.EndDt = taxconfig.EndDt;

                    empRec.InsCode = 1;                                 //TODO: MAKE DYNAMIC - PROBABLY FROM A TABLE
                    empRec.InsAct = act.TotalInsurancePremium;

                    empRec.UlipCode = 4;
                    empRec.UlipAct = act.TotalUlip;

                    empRec.MfCode = 6;
                    empRec.MfAct = act.TotalMutualFund;

                    empRec.PpfCode = 7;
                    empRec.PpfAct = act.TotalPpfAmt;

                    empRec.NscCode = 11;
                    empRec.NscAct = act.TotalNscAmount;

                    empRec.HomeLoanCode = 14;
                    empRec.HomeLoanAct = act.HouseLoanPrincipal;

                    empRec.NotifiedMfCode = 18;
                    empRec.NotifiedMfAct = act.TotalSukanya;

                    empRec.Child1Code = 24;
                    empRec.Child1Act = act.TuitionFeeChild1;

                    empRec.Child2Code = 25;
                    empRec.Child2Act = act.TuitionFeeChild2;

                    empRec.TermDepoCode = 26;
                    empRec.TermDepoAct = act.TotalBankDepositAmount;

                    empRec.Total80C = empRec.InsAct + empRec.UlipAct + empRec.MfAct + empRec.PpfAct + empRec.NscAct +
                                      empRec.HomeLoanAct + empRec.NotifiedMfAct + empRec.Child1Act + empRec.Child2Act +
                                      empRec.TermDepoAct;


                    empRec.LongTermMf = 0;          //TODO: CONFIRM
                    empRec.MedicalPremium = act.MedicalPremiumSelf + act.MedicalPremiumParents;
                    empRec.EduLoanInterest = act.EducationLoanInterest;
                    empRec.Nps = act.NationalPensionScheme;
                    empRec.InterestOnLoan = act.InterestOnLoan;
                    empRec.RentReceived = act.RentalIncomePerMonth;
                    empRec.BankName = act.LoanBank;
                    empRec.BankPan = act.LoanBankPan;
                    empRec.AccomodationType = empobj.CompanyAcc ? "3" : "1";

                    empRec.RentPaidAprilAct = act.RentDetails.Count > 0 ? act.RentDetails[0].April : 0;
                    empRec.RentPaidAct = act.TotalRentPaid;
                    empRec.LandLordName = act.LandLordName;
                    empRec.LandLordPan = act.LandLordPan;

                    empRec.RajivGandhiEquity = act.RajivGandhiEquity;
                    empRec.MedicalPremiumSelf = act.MedicalPremiumSelf;
                    empRec.MedicalPremiumParents = act.MedicalPremiumParents;
                    empRec.MedicalPreventiveHealthCheckup = act.MedicalPreventiveHealthCheckup;
                    empRec.PhysicalDisability = act.PhysicalDisability;
                    empRec.SevereDisability = act.SevereDisability;

                }

                report.Add(empRec);
            }

            return Ok(report);
        }

        public IHttpActionResult GetBankNames(string flag)
        {
            var banks = _context.Banks.ToList().Select(Mapper.Map<Banks, BankDto>);
            return Ok(banks);
        }

        /// <summary>
        /// Create/Update Tax declaration of employee
        /// </summary>
        /// <param name="requestData">Data in form of TaxDeclaration json object </param>
        /// <returns></returns>
        [HttpPost]
        public IHttpActionResult CreateTaxDeclaration([FromBody] object requestData)
        {
            var sentData = JsonConvert.DeserializeObject<TaxDeclarationDto>(requestData.ToString());

            if (!ModelState.IsValid)
                return BadRequest("Model state is invalid.");

            var found = _context.TaxDeclarations
                .Any(t => t.EmpUnqId == sentData.EmpUnqId &&
                    t.YearMonth == sentData.YearMonth &&
                    t.ActualFlag == sentData.ActualFlag);


            TaxDeclarations taxDeclaration;

            if (!found)
            {
                taxDeclaration = new TaxDeclarations();
            }
            else
            {
                taxDeclaration = _context.TaxDeclarations
                    .FirstOrDefault(t => t.EmpUnqId == sentData.EmpUnqId &&
                              t.YearMonth == sentData.YearMonth &&
                              t.ActualFlag == sentData.ActualFlag);
            }

            var tmp = Mapper.Map<TaxDeclarationDto, TaxDeclarations>(sentData);

            foreach (PropertyInfo prop in tmp.GetType().GetProperties())
            {
                if (!prop.CanRead) continue;            //if property is readable... 

                object value = prop.GetValue(tmp, null);
                prop.SetValue(taxDeclaration, value);
            }

            if (taxDeclaration == null) return BadRequest("Error!!!");

            foreach (var detail in taxDeclaration.InsuranceDetails.ToList())
            {
                taxDeclaration.InsuranceDetails.Remove(detail);
            }

            foreach (var detail in taxDeclaration.MutualFundDetails.ToList())
            {
                taxDeclaration.MutualFundDetails.Remove(detail);
            }

            foreach (var detail in taxDeclaration.NscDetails.ToList())
            {
                taxDeclaration.NscDetails.Remove(detail);
            }

            foreach (var detail in taxDeclaration.PpfDetails.ToList())
            {
                taxDeclaration.PpfDetails.Remove(detail);
            }

            foreach (var detail in taxDeclaration.BankDeposits.ToList())
            {
                taxDeclaration.BankDeposits.Remove(detail);
            }

            foreach (var detail in taxDeclaration.UlipDetails.ToList())
            {
                taxDeclaration.UlipDetails.Remove(detail);
            }

            foreach (var detail in taxDeclaration.SukanyaDetails.ToList())
            {
                taxDeclaration.SukanyaDetails.Remove(detail);
            }

            foreach (var detail in taxDeclaration.RentDetails.ToList())
            {
                taxDeclaration.RentDetails.Remove(detail);
            }

            if (sentData.InsuranceDetails.Count > 0)
            {
                foreach (var insuranceDto in sentData.InsuranceDetails)
                {
                    taxDeclaration.InsuranceDetails.Add(
                        Mapper.Map<TaxDetailsInsuranceDto, TaxDetailsInsurance>(insuranceDto));
                }
            }
            if (sentData.MutualFundDetails.Count > 0)
            {
                foreach (var mfDto in sentData.MutualFundDetails)
                {
                    taxDeclaration.MutualFundDetails.Add(
                        Mapper.Map<TaxDetailsMutualFundsDto, TaxDetailsMutualFunds>(mfDto));
                }
            }
            if (sentData.NscDetails.Count > 0)
            {
                foreach (var nscDto in sentData.NscDetails)
                {
                    taxDeclaration.NscDetails.Add(
                        Mapper.Map<TaxDetailsNscDto, TaxDetailsNsc>(nscDto));
                }
            }
            if (sentData.PpfDetails.Count > 0)
            {
                foreach (var ppfDto in sentData.PpfDetails)
                {
                    taxDeclaration.PpfDetails.Add(
                        Mapper.Map<TaxDetailsPpfDto, TaxDetailsPpf>(ppfDto));
                }
            }
            if (sentData.BankDeposits.Count > 0)
            {
                foreach (var bankDto in sentData.BankDeposits)
                {
                    taxDeclaration.BankDeposits.Add(
                        Mapper.Map<TaxDetailsBankDepositDto, TaxDetailsBankDeposit>(bankDto));
                }
            }
            if (sentData.UlipDetails.Count > 0)
            {
                foreach (var ulipDto in sentData.UlipDetails)
                {
                    taxDeclaration.UlipDetails.Add(
                        Mapper.Map<TaxDetailsUlipDto, TaxDetailsUlip>(ulipDto));
                }
            }
            if (sentData.SukanyaDetails.Count > 0)
            {
                foreach (var sukanyaDto in sentData.SukanyaDetails)
                {
                    taxDeclaration.SukanyaDetails.Add(
                        Mapper.Map<TaxDetailsSukanyaDto, TaxDetailsSukanya>(sukanyaDto));
                }
            }
            if (sentData.RentDetails.Count > 0)
            {
                foreach (var rentDto in sentData.RentDetails)
                {
                    taxDeclaration.RentDetails.Add(
                        Mapper.Map<TaxDetailsRentDto, TaxDetailsRent>(rentDto));
                }
            }

            if (!found)
            {
                _context.TaxDeclarations.Add(taxDeclaration);
                _context.Entry(taxDeclaration).State = EntityState.Added;
            }

            _context.SaveChanges();
            return Ok(Mapper.Map<TaxDeclarations, TaxDeclarationDto>(taxDeclaration));
        }

    }
}
