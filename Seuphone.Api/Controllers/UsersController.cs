﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seuphone.Api.Data;
using Seuphone.Api.DTO;
using Seuphone.Api.Helpers;
using Seuphone.Api.IServices;
using Seuphone.Api.Models;
using Seuphone.Api.Services;

namespace Seuphone.Api.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {

        private IUserService _userService;
        private MailService _mailService;
        private readonly SeuphoneApiContext _context;

        public UsersController(IUserService userService, SeuphoneApiContext context, MailService mailService)
        {
            _userService = userService;
            _context = context;
            _mailService = mailService;
        }

        /// <summary>
        ///     Autenticação do usuário no sistema
        /// </summary>
        /// <response code="200">O usuário foi autenticado com sucesso.</response>
        /// <param name="model">E-mail e senha</param>
        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody] Authentication model)
        {

            var encryptedPassword = CommonMethods.WordEncrypter(model.Password);

            var user = _userService.Authenticate(model.Email, encryptedPassword);
            if (user == null) return BadRequest("Usuário e/ou senha incorretos.");
            return Ok(user.Token);
        }


        // GET: api/Users
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            var users = await _context.User
                .Include(user => user.UserRoles)
                    .ThenInclude(userRoles => userRoles.Role)
                .ToListAsync();

            foreach(User user in users)
            {
                user.Password = null;
                user.ConfirmPassword = null;
                user.Token = null;
            }

            return users;
        }

        // GET: api/Users/5
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.User
                .Include(user => user.UserRoles)
                    .ThenInclude(userRoles => userRoles.Role)
                .SingleOrDefaultAsync(user => user.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            user.Password = null;
            user.ConfirmPassword = null;
            user.Token = null;

            return user;
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [Authorize]
        [HttpPut("{id}/address")]
        public async Task<IActionResult> UpdateUserAddress(int id, AddressDTO user)
        {
            //if (id != user.Id)
            //{
            //    return BadRequest();
            //}

            var findUser = _context.User.Find(id);

            if(findUser != null )
            {
                findUser.Address = user.Address;
                findUser.City = user.City;
                findUser.District = user.District;
                findUser.HouseNumber = user.HouseNumber;
                findUser.State = user.State;
                findUser.ZipCode = user.ZipCode;


                _context.Entry(findUser).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

            }

           

            return NoContent();
        }

                // PUT: api/Users/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [Authorize]
        [HttpPut("{id}/password")]
        public async Task<IActionResult> UpdateUserPassword(int id, PasswordDTO userPassword)
        {

            var findUser = _context.User.Find(id);

            if(findUser != null )
            {
                var decryptedPassword = CommonMethods.WordDecrypter(findUser.Password);

                if(userPassword.OldPassword.Equals(decryptedPassword))
                {
                    var encryptedNewPassword = CommonMethods.WordEncrypter(userPassword.NewPassword);
                    var encryptedConfirmNewPassword = CommonMethods.WordEncrypter(userPassword.ConfirmNewPassword);

                    findUser.Password = encryptedNewPassword;
                    findUser.ConfirmPassword = encryptedConfirmNewPassword;

                } else
                {
                    return BadRequest("A senha atual informada está incorreta.");
                }


                _context.Entry(findUser).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

            }

           

            return NoContent();
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("password/reset")]
        public async Task<IActionResult> ResetUserPassword([FromBody]PasswordReset pr)
        {
 
            var findUser = _context.User
                .Where(u => u.Email == pr.Email)
                .SingleOrDefault();

            if(findUser == null)
            {
                return NotFound("E-mail de cadastro não encontrado, tente novamente.");
            }

            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            int length = 7;
            Random random = new Random();

            string randomPassword = new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());


            var encryptedNewPassword = CommonMethods.WordEncrypter(randomPassword);
            var encryptedConfirmNewPassword = CommonMethods.WordEncrypter(randomPassword);

            findUser.Password = encryptedNewPassword;
            findUser.ConfirmPassword = encryptedConfirmNewPassword;


            _context.Entry(findUser).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                _mailService.ResetUserPasswordMail(findUser, randomPassword);

            }
            catch (DbUpdateConcurrencyException)
            {
                if (findUser == null)
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Users
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {

            try
            {
                var checkCPF = await _context.User.Where(u => u.CPF == user.CPF).FirstOrDefaultAsync();

                var checkEmail = await _context.User.Where(u => u.Email == user.Email).FirstOrDefaultAsync();

                if (checkCPF != null)
                {
                    return BadRequest("O CPF digitado já possui cadastro no sistema.");
                }

                if (checkEmail != null)
                {
                    return BadRequest("O E-mail digitado já possui cadastro no sistema.");
                }

                var encryptedPassword = CommonMethods.WordEncrypter(user.Password);
                var encryptedConfirmPassword = CommonMethods.WordEncrypter(user.ConfirmPassword);

                user.Password = encryptedPassword;
                user.ConfirmPassword = encryptedConfirmPassword;


                Role role = _context.Role.Where(r => r.RoleName == "ROLE_CLIENTE").FirstOrDefault();

                List<UserRole> roles = new List<UserRole>() {  };
                roles.Add(new UserRole() { Role = role, User = user });

                user.UserRoles = roles;

                _context.User.Add(user);
                await _context.SaveChangesAsync();

                user.Password = null;
                user.ConfirmPassword = null;

                _mailService.CreateUserMail(user);

                return CreatedAtAction("GetUser", new { id = user.Id }, user);

            }
            catch (Exception ex)
            {
                return BadRequest("Ocorreu um erro. " + ex);
            }



        }

        // DELETE: api/Users/5
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<User>> DeleteUser(int id)
        {
            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.User.Remove(user);
            await _context.SaveChangesAsync();

            return user;
        }

        [HttpPost("admin")]
        public async Task<ActionResult<User>> CreateUserAdm(User user)
        {

            try
            {
                var checkCPF = await _context.User.Where(u => u.CPF == user.CPF).FirstOrDefaultAsync();

                var checkEmail = await _context.User.Where(u => u.Email == user.Email).FirstOrDefaultAsync();

                if (checkCPF != null)
                {
                    return BadRequest("O CPF digitado já possui cadastro no sistema.");
                }

                if (checkEmail != null)
                {
                    return BadRequest("O E-mail digitado já possui cadastro no sistema.");
                }

                _context.User.Add(user);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetUser", new { id = user.Id }, user);

            }
            catch (Exception ex)
            {
                return BadRequest("Ocorreu um erro. " + ex);
            }

        }

        [Authorize]
        [HttpPut("admin/{id}")]
        public async Task<IActionResult> UpdateAdminUser(int id, UserEditDTO user)
        {
            //if (id != user.Id)
            //{
            //    return BadRequest();
            //}

            var findUser = _context.User
                  .Include(user => user.UserRoles)
                   .Where(user => user.Id == id)
                   .SingleOrDefault();


                if (findUser != null)
            {

                if (user.Password != "" || user.Password != null)
                {

                    var encryptedPassword = CommonMethods.WordEncrypter(user.Password);

                    user.Password = encryptedPassword;

                    findUser.Password = user.Password;                 

                }
                findUser.Address = user.Address;
                findUser.City = user.City;
                findUser.District = user.District;
                findUser.HouseNumber = user.HouseNumber;
                findUser.State = user.State;
                findUser.ZipCode = user.ZipCode;
                findUser.UserRoles = user.UserRoles;

                var roleObject = user.UserRoles;

                List<UserRole> roles = new List<UserRole>() { };

                foreach (var item in roleObject)
                {
                    var currentRole = item.RoleId;
                    var roleDataBase = _context.Role.Where(r => r.Id == currentRole).SingleOrDefault();

                    roles.Add(new UserRole() { Role = roleDataBase, User = findUser });
                }

                user.UserRoles = roles;

                _context.Entry(findUser).State = EntityState.Modified;
    
                _context.Update(findUser);

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

            }



            return NoContent();
        }

        private bool UserExists(int id)
        {
            return _context.User.Any(e => e.Id == id);
        }
    }
}
