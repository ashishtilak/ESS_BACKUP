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


            var empAdd = _context.EmpAddress
                .SingleOrDefault(e => e.EmpUnqId == dto.EmpUnqId);

            if (empAdd != null)
            {
                empAdd.PreAdd1 = dto.PreAdd1;
                empAdd.PreAdd2 = dto.PreAdd2;
                empAdd.PreAdd3 = dto.PreAdd3;
                empAdd.PreAdd4 = dto.PreAdd4;
                empAdd.PreDistrict = dto.PreDistrict;
                empAdd.PreCity = dto.PreCity;
                empAdd.PreState = dto.PreState;
                empAdd.PrePin = dto.PrePin;
                empAdd.PrePhone = dto.PrePhone;
                empAdd.PreResPhone = dto.PreResPhone;
            }
            else
            {
                var emp = _context.Employees
                    .SingleOrDefault(e => e.EmpUnqId == dto.EmpUnqId);

                if (emp == null)
                    return BadRequest("Invalid employee");

                EmpAddress newAdd = new EmpAddress
                {
                    EmpUnqId = dto.EmpUnqId,
                    PreAdd1 = dto.PreAdd1,
                    PreAdd2 = dto.PreAdd2,
                    PreAdd3 = dto.PreAdd3,
                    PreAdd4 = dto.PreAdd4,
                    PreDistrict = dto.PreDistrict,
                    PreCity = dto.PreCity,
                    PreState = dto.PreState,
                    PrePin = dto.PrePin,
                    PrePhone = dto.PrePhone,
                    PreResPhone = dto.PreResPhone
                };

                _context.EmpAddress.Add(newAdd);

            }

            _context.SaveChanges();

            return Ok();

        }
    }
}
