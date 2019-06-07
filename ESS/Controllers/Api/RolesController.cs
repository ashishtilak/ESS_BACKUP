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
    public class RolesController : ApiController
    {
        private ApplicationDbContext _context;

        public RolesController()
        {
            _context = new ApplicationDbContext();
        }

        public IHttpActionResult GetRoleAuth(int roleId)
        {
            var roleAuth = _context.RoleAuths.Where(r => r.RoleId == roleId)
                .ToList()
                .Select(Mapper.Map<RoleAuth, RoleAuthDto>);

            return Ok(roleAuth);
        }
    }
}
