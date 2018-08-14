using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using ESS.Dto;
using ESS.Models;
using Newtonsoft.Json;

namespace ESS.Controllers.Api
{
    public class EmpAddressController : ApiController
    {
        private ApplicationDbContext _context;

        public EmpAddressController()
        {
            _context = new ApplicationDbContext();
        }


        public IHttpActionResult GetEmpAddress(string empUnqId)
        {
            var empAdd = _context.EmpAddress
                .OrderByDescending(e => e.Counter)
                .Select(Mapper.Map<EmpAddress, EmpAddressDto>)
                .FirstOrDefault(e => e.EmpUnqId == empUnqId);

            if (empAdd == null)
                return BadRequest("Invalid employee.");


            return Ok(empAdd);
        }

        [HttpPost]
        public IHttpActionResult UpdateEmpAddress([FromBody] object requestData)
        {
            var dto = JsonConvert.DeserializeObject<EmpAddressDto>(requestData.ToString());
            if (!ModelState.IsValid)
                return BadRequest();

            int maxId = 0;
            try
            {
                maxId = _context.EmpAddress.Where(e => e.EmpUnqId == dto.EmpUnqId).Max(e => e.Counter);
            }
            catch
            {
                maxId = 0;
            }

            maxId++;

            var empAdd = _context.EmpAddress
                .OrderByDescending(e => e.Counter)
                .FirstOrDefault(e => e.EmpUnqId == dto.EmpUnqId);


            if (empAdd == null)
            {
                var emp = _context.Employees
                    .SingleOrDefault(e => e.EmpUnqId == dto.EmpUnqId);

                if (emp == null)
                    return BadRequest("Invalid employee");
            }
            else
            {
                //Check if data is changed or not
                if (

                    empAdd.PreAdd1 == dto.PreAdd1 &&
                    empAdd.PreAdd2 == dto.PreAdd2 &&
                    empAdd.PreAdd3 == dto.PreAdd3 &&
                    empAdd.PreAdd4 == dto.PreAdd4 &&
                    empAdd.PreDistrict == dto.PreDistrict &&
                    empAdd.PreCity == dto.PreCity &&
                    empAdd.PreState == dto.PreState &&
                    empAdd.PrePin == dto.PrePin &&
                    empAdd.PrePhone == dto.PrePhone &&
                    empAdd.PreResPhone == dto.PreResPhone &&
                    empAdd.PreEmail == dto.PreEmail

                )
                {
                    return BadRequest("NO DATA CHANGED.");
                }
            }


            EmpAddress newAdd = new EmpAddress
            {
                EmpUnqId = dto.EmpUnqId,
                Counter = maxId,
                PreAdd1 = dto.PreAdd1,
                PreAdd2 = dto.PreAdd2,
                PreAdd3 = dto.PreAdd3,
                PreAdd4 = dto.PreAdd4,
                PreDistrict = dto.PreDistrict,
                PreCity = dto.PreCity,
                PreState = dto.PreState,
                PrePin = dto.PrePin,
                PrePhone = dto.PrePhone,
                PreResPhone = dto.PreResPhone,
                PreEmail = dto.PreEmail,
                UpdDt = DateTime.Now
            };

            _context.EmpAddress.Add(newAdd);



            _context.SaveChanges();

            return Ok();

        }
    }
}
