using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;

namespace ESS.Controllers.Api
{
    public class MedDependentController : ApiController
    {
        private readonly ApplicationDbContext _context;

        public MedDependentController()
        {
            _context = new ApplicationDbContext();
        }

        public IHttpActionResult GetDependents(bool mode, int policyYear, string empUnqId=null)
        {
            // MODE will be true if all records of all employees are requested
            // If Mode is false, then empUnqId must have been provided
            try
            {
                List<MedDependentDto> dep;
                List<MedEmpUhidDto> uhids;

                //get dependents 
                
                if (!mode)  //for single emp
                {
                    if (empUnqId == null)
                        return BadRequest("Provide empunqid.");

                    dep = _context.MedDependents
                        .Where(d => d.EmpUnqId == empUnqId)
                        .AsEnumerable()
                        .Select(Mapper.Map<MedDependent, MedDependentDto>)
                        .ToList();

                    if (dep.Count == 0)
                        return BadRequest("No records found.");

                    // get all corresponding UhIds for provided year

                    uhids = _context.MedEmpUhids
                        .Where(e => e.EmpUnqId == empUnqId && e.PolicyYear == policyYear)
                        .AsEnumerable()
                        .Select(Mapper.Map<MedEmpUhid, MedEmpUhidDto>)
                        .ToList();

                }
                else
                {
                    dep = _context.MedDependents
                        .Where(d => d.Active)
                        .AsEnumerable()
                        .Select(Mapper.Map<MedDependent, MedDependentDto>)
                        .ToList();
                    
                    if (dep.Count == 0)
                        return BadRequest("No records found.");

                    // get all corresponding UhIds for provided year

                    var empList = dep.Select(d => d.EmpUnqId).ToArray();

                    uhids = _context.MedEmpUhids
                        .Where(e => e.PolicyYear == policyYear 
                                    && empList.Contains(e.EmpUnqId)
                                    && e.Active)
                        .AsEnumerable()
                        .Select(Mapper.Map<MedEmpUhid, MedEmpUhidDto>)
                        .ToList();
                }

                // attach uhid to dto

                foreach (MedDependentDto dto in dep)
                {
                    MedEmpUhidDto uhid = uhids.FirstOrDefault(u => u.EmpUnqId == dto.EmpUnqId && u.DepSr == dto.DepSr);
                    if (dto.UhIds == null)
                        dto.UhIds = new List<MedEmpUhidDto>();
                    dto.UhIds.Add(uhid);
                }

                return Ok(dep);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }
        }
    }
}
