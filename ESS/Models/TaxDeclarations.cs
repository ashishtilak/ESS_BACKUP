using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ESS.Models
{
    public class TaxDeclarations
    {
        [Key]
        public int YearMonth { get; set; }

        [Key]
        [StringLength(10)]
        public string EmpUnqId { get; set; }

        public float RentPaidPerMonth { get; set; }
        public string RentHouseAddress { get; set; }
        public string LandLordName { get; set; }
        public string LandLordPan { get; set; }

        public float PrevCompSalary { get; set; }
        public float PrevCompTds { get; set; }

        public float Ppf { get; set; }
        public float BankDeposit { get; set; }
        public float InsurancePremiums { get; set; }
        public float Nscs { get; set; }
        public float MutualFunds { get; set; }

        public float HouseLoanPrincipal { get; set; }
        public string Child1Name { get; set; }
        public float TuitionFeeChild1 { get; set; }
        public string Child2Name { get; set; }
        public float TuitionFeeChild2 { get; set; }
        public float NotifiedPensionScheme { get; set; }
        public string OthersDesc { get; set; }
        public float OthersAmount { get; set; }

        public float RajivGandhiEquity { get; set; }
        public float MedicalPremiumSelf { get; set; }
        public float MedicalPremiumParents { get; set; }
        public float MedicalPremiumParentsAge { get; set; }
        public float MedicalPreventiveHealthCheckup { get; set; }
        public float EducationLoanInterest { get; set; }
        public float PhysicalDisability { get; set; }
        public float NationalPensionScheme { get; set; }

        public string PropertyAddress { get; set; }
        public string PropertyStatus { get; set; }
        public string LoanBank { get; set; }
        public float LoanAmount { get; set; }
        public DateTime? LoanDate { get; set; }
        public string Purpose { get; set; }
        public DateTime? ConstructionCompDate { get; set; }
        public DateTime? PossessionDate { get; set; }
        public string Ownership { get; set; }
        public string JointOwnerName { get; set; }
        public string JointOwnerRelation { get; set; }
        public string JointOwnerShare { get; set; }
        public float RentalIncomePerMonth { get; set; }
        public float InterestOnLoan { get; set; }
        public float InterestPreConstruction { get; set; }
        public string OtherIncomeDesc { get; set; }
        public float OtherIncomeAmount { get; set; }

    }
}