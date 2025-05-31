using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto.Generators;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YallaR7la.Data;
using YallaR7la.Data.Models;
using YallaR7la.DtoModels;

namespace YallaR7la.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Policy = "AdminOnly")]
    public class AdminsController : ControllerBase
    {
        private readonly AppDbContext _appDbContext;
        //private readonly UserManager<Admin> _adminManager;
        //private readonly UserManager<BusinessOwner> _ownerManager;
        //private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        public AdminsController(AppDbContext appDbContext , IConfiguration configuration , IMemoryCache memoryCache , RoleManager<IdentityRole> roleManager)
        {
            _appDbContext = appDbContext;
            //_adminManager = adminManager;
            //_ownerManager = ownerManager;
            _configuration = configuration;
            _memoryCache = memoryCache;
            //_roleManager = roleManager;
            
        }



        //------------------------------------------------
        
        


        #region GetAdminWithId

        [HttpGet("GetAdminWithId/{adminId}")]
        public async Task<IActionResult> GetAdminWithId(string adminId)
        {
            var adminDetails = await _appDbContext.Admins
                .Where(a => a.AdminId == adminId)
                .Select(a => new AdminDto
                {
                    Name = a.Name,
                    Email = a.Email,
                    PhoneNumper = a.PhoneNumper,
                    CreatedAt = a.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (adminDetails == null)
            {
                return NotFound(new { Message = "Admin not found" });
            }

            return Ok(adminDetails);
        }
        // --------------
        #endregion


        #region Get Admin Info
        
            [HttpGet("GetAdminInfo")]
            [Authorize]
            
            public async Task<IActionResult> GetAdminInfo()
            {
                var currutAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (currutAdminId == null)
                    return Forbid("You don't have acess !");
                var admin =await _appDbContext.Admins.Where(a => a.AdminId == currutAdminId).Select(a => new
                {
                    a.Name,
                    a.Email,
                    a.PhoneNumper,
                    a.ImageData
                }).FirstOrDefaultAsync();
            
                if (admin == null) return NotFound("This Admo not found!");
            
                return Ok(admin);
            }
        
        #endregion 

        #region Get All Admin
        [HttpGet("GetAllAdmin")]
        public async Task<IActionResult> GetAllAdmin()
        {
            var allAdmin = await _appDbContext.Admins
                .Select(a => new AdminDto
                {
                    Name = a.Name,
                    Email = a.Email,
                    PhoneNumper = a.PhoneNumper,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            if (allAdmin == null)
            {
                return NotFound(new { Message = "Admin not found" });
            }

            return Ok(allAdmin);
        }
        #endregion
        //-------------------

        #region GetAdminDetails
        [HttpGet("GetAdminDetails/{adminId}")]
        public async Task<IActionResult> GetAdminDetails(string adminId)
        {
            var adminDetails = await _appDbContext.Admins.Where(a => a.AdminId == adminId).SingleOrDefaultAsync();
            if (adminDetails == null) {
                return NotFound(new { Message = "This Admin is not foud!" });
                    }
            return Ok(adminDetails);
        }
        #endregion


        #region Add Admin 
        //[HttpPost("AddAdmin")]
        //[AllowAnonymous]

        //public async Task<IActionResult> AddAdmin(MdlAdmin mdlAdmin)
        //{
        //    //var currrentadmin = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    //if (currrentadmin == null || currrentadmin != adminId)
        //    //{
        //    //    return Forbid("You do not have access to add admin!");
        //    //}


        //    using var stream = new MemoryStream();
        //    await mdlAdmin.ImageData.CopyToAsync(stream);
        //    var admin = new Admin()
        //    {
        //        Name = mdlAdmin.Name,
        //        Email = mdlAdmin.Email,
        //        Password = mdlAdmin.Password,
        //        PhoneNumper = mdlAdmin.PhoneNumper,

        //        ImageData = stream.ToArray(),
        //        UniqeImageId = Guid.NewGuid(),
        //    };

        //    await _appDbContext.AddAsync(admin);
        //    await _appDbContext.SaveChangesAsync();
        //    return Ok(admin);

        //}

        #endregion


        #region  ResetPassword



        [Authorize(Roles = "Admin")]
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] MdlResetPassword model)
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            var admin = await _appDbContext.Admins.FindAsync(adminId);
            if (admin == null) return NotFound("Admin not found!");

            var hasher = new PasswordHasher<Admin>();
            var result = hasher.VerifyHashedPassword(admin, admin.PasswordHash, model.OldPassword);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Old password is incorrect!");

            admin.PasswordHash = hasher.HashPassword(admin, model.NewPassword);
            admin.UpdatedAt = DateTime.UtcNow;

            try
            {
                _appDbContext.Admins.Update(admin);
                await _appDbContext.SaveChangesAsync();
                return Ok("Password has been reset successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        #endregion


        #region Update Admin Info
        [HttpPut("Update_Admin_Info/{adminId}")]
        public async Task<IActionResult> UpdateAdminInfo(string adminId, [FromForm] MdlAdmin mdlAdmin)
        {
            var admin = await _appDbContext.Admins.FindAsync(adminId);
            var oldImage = admin.ImageData;
            if (admin == null) { return NotFound("this admin not found!"); }
            //var existingAdminData = new
            //{
            //    admin.Name,
            //    admin.Email,
            //    admin.ImageData,
            //    admin.PhoneNumper

            //};
            var adminDetails = await _appDbContext.Admins.Where(a => a.AdminId == adminId).SingleOrDefaultAsync();
            if (adminDetails == null)
            {
                return NotFound(new { Message = "This Admin is not foud!" });
            }
            //else
            //    return Ok(adminDetails);

            if (mdlAdmin.ImageData != null)
            {
                using var stream = new MemoryStream();
                await mdlAdmin.ImageData.CopyToAsync(stream);
                admin.ImageData = stream.ToArray();
            }
            else
            {
                admin.ImageData = oldImage ;
            }
            admin.Name = mdlAdmin.Name;
            admin.Email = mdlAdmin.Email;
            
            
            await _appDbContext.SaveChangesAsync();
            return Ok(new
            {
                Message = "Admin data updated successfully!",
                //OldData = existingAdminData,
                UpdatedData = new
                {
                    admin.AdminId,
                    admin.Name,
                    admin.Email,
                    admin.UpdatedAt
                }
            });
        }

        #endregion


        //----------------------------------
        #region Update Admin Info3
        //[HttpPut("Update_Admin_Info3/{adminId}")]
        //public async Task<IActionResult> UpdateAdminInfo3(string adminId, [FromForm] MdlAdmin mdlAdmin)
        //{
        //    //  Retrieve admin data from the database
        //    var admin = await _appDbContext.Admins.FindAsync(adminId);
        //    if (admin == null)
        //    {
        //        return NotFound(new { Message = "This admin was not found!" });
        //    }

        //    //  Send existing data before updating
        //    var existingData = new
        //    {
        //        admin.AdminId,
        //        admin.Name,
        //        admin.Email,
        //        admin.UpdatedAt
        //    };

        //    //  If no new data is provided, return the existing data
        //    if (mdlAdmin.Name == null && mdlAdmin.Email == null && mdlAdmin.Password == null && mdlAdmin.ImageData == null)
        //    {
        //        return Ok(new
        //        {
        //            Message = "Current admin data retrieved successfully!",
        //            ExistingData = existingData
        //        });
        //    }

            
        //    byte[] oldImageData = admin.ImageData;

            
        //    admin.Name = !string.IsNullOrEmpty(mdlAdmin.Name) ? mdlAdmin.Name : admin.Name;
        //    admin.Email = !string.IsNullOrEmpty(mdlAdmin.Email) ? mdlAdmin.Email : admin.Email;
        //    admin.Password = !string.IsNullOrEmpty(mdlAdmin.Password) ? mdlAdmin.Password : admin.Password; 
        //    if (mdlAdmin.ImageData != null && mdlAdmin.ImageData.Length > 0)
        //    {
        //        using var stream = new MemoryStream();
        //        await mdlAdmin.ImageData.CopyToAsync(stream);
        //        admin.ImageData = stream.ToArray();
        //    }
        //    else
        //    {
        //        admin.ImageData = oldImageData; 
        //    }

            
        //    admin.UpdatedAt = DateTime.Now;
        //    await _appDbContext.SaveChangesAsync();

           
        //    return Ok(new
        //    {
        //        Message = "Admin data updated successfully!",
        //        UpdatedData = new
        //        {
        //            admin.AdminId,
        //            admin.Name,
        //            admin.Email,
        //            admin.UpdatedAt
        //        }
        //    });
        //}

        #endregion


        // Method delete

        #region Delate Admin
        [HttpDelete("Delate_Admin/{adminId}")]
        public async Task<IActionResult> DeleteAdmin(string adminId)
        {
            var adminToDelete = await _appDbContext.Admins.FindAsync(adminId);
            if (adminToDelete == null)
            {
                return NotFound("This Admin is not found , maybe delete before!");
            }
             _appDbContext.Remove(adminToDelete);
            await _appDbContext.SaveChangesAsync();
            return Ok("admin is delete!");
        }
        #endregion
        // Owners Method




        #region add Owners Acount
        [HttpPost("AddOwner")]
        //[Authorize]
        public async Task<IActionResult> AddOwner([FromForm] MdlOwner mdlOwner)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if the email is founded before
            bool exists = await _appDbContext.BusinessOwners.AnyAsync(o => o.Email == mdlOwner.Email);
            if (exists)
                return Conflict("An owner with this email already exists.");

            // Get the image for owner
            byte[] imageData = null;
            if (mdlOwner.ImageData != null && mdlOwner.ImageData.Length > 0)
            {
                using var stream = new MemoryStream();
                await mdlOwner.ImageData.CopyToAsync(stream);
                imageData = stream.ToArray();
            }

            // Get AdminId from token
            //var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            //if (string.IsNullOrEmpty(adminId))
            //    return Unauthorized("Invalid admin token.");

            // Git the New Owner Data
            var owner = new BusinessOwner
            {
                Name = mdlOwner.Name,
                Email = mdlOwner.Email,
                PhoneNumper = mdlOwner.PhoneNumper,
                TimeAdd = DateTime.UtcNow,
                ImageData = imageData,
                UniqueIdImage = Guid.NewGuid(),
                
            };

            // Hashing password
            var hasher = new PasswordHasher<BusinessOwner>();
            owner.PasswordHash = hasher.HashPassword(owner, mdlOwner.Password);

            // Save the New Owner
            await _appDbContext.BusinessOwners.AddAsync(owner);
            await _appDbContext.SaveChangesAsync();

            return Ok(new
            {
                Message = "Owner added successfully.",
                OwnerId = owner.BusinessOwnerId,
                owner.Name,
                owner.Email,
                owner.PhoneNumper
            });
        }

        #endregion

        // ----------------- Get all Owners -------------------
        #region Get All Owners
        [HttpGet("GetAllOwners")]
        [Authorize(policy: "AdminOnly")]
        public async Task<IActionResult> GetAllOwners()
        {
            var owners = await _appDbContext.BusinessOwners.ToListAsync();
            return Ok(owners);
        }
        #endregion

        // Create acount for Owner
        #region Creat Owner Acount

        //[HttpPost("CreatOwnerAcount")]
        //[Authorize(policy: "AdminOnly")]
        //public async Task<IActionResult> AddNewOwner( [FromForm] MdlOwner newOwner)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    if (string.IsNullOrEmpty(currentAdminId))
        //        return Forbid("You do not have access to add an owner!");

        //    byte[] imageData;
        //    using (var stream = new MemoryStream())
        //    {
        //        await newOwner.ImageData.CopyToAsync(stream);
        //        imageData = stream.ToArray();
        //    }

        //    // Optionally hash password here
        //    var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newOwner.Password);

        //    var owner = new BusinessOwner
        //    {
        //        Name = newOwner.Name,
        //        Email = newOwner.Email,
        //        PhoneNumper = newOwner.PhoneNumper,
        //        TimeAdd = DateTime.Now,
        //        ImageData = imageData,
        //        UniqueIdImage = Guid.NewGuid(),
        //        PasswordHash = hashedPassword, // make sure this column exists
                
        //    };

        //    await _appDbContext.BusinessOwners.AddAsync(owner);
        //    await _appDbContext.SaveChangesAsync();

        //    return Ok("Owner added successfully.");
        //}

        #endregion
        // ----------------- Delete Owner -------------------
        #region Delate Owner
        [HttpDelete("Delate_Owner/{ownerId}")]
        public async Task<IActionResult> DeleteOwner(string ownerId)
        {
            var ownerToDelete = await _appDbContext.BusinessOwners.FindAsync(ownerId);
            if (ownerToDelete == null)
            {
                return NotFound("This Owner is not found , maybe delete before!");
            }
            _appDbContext.Remove(ownerToDelete);
            await _appDbContext.SaveChangesAsync();
            return Ok("Owner is delete!");
        }

        #endregion

        // --------------- Update Owner -------------
        #region Update Owner 
        
        [HttpPut("UpdateOwner/{ownerId}")]
        public async Task<IActionResult> UpdateOwner(string ownerId, [FromForm] MdlOwner mdlOwner)
        {
            var owner = await _appDbContext.BusinessOwners.FindAsync(ownerId);
            if (owner == null)
            {
                return NotFound(new { Message = "This Owner was not found!" });
            }

            var existingData = new
            {
                owner.Name,
                owner.Email,
                owner.PhoneNumper
            };

            if (mdlOwner.Name == null && mdlOwner.Email == null && mdlOwner.PhoneNumper == null && mdlOwner.Password == null && mdlOwner.ImageData == null)
            {
                return Ok(new
                {
                    Message = "Current owner data retrieved successfully!",
                    ExistingData = existingData
                });
            }

            // Save old image if new one not provided
            byte[] oldImageData = owner.ImageData;

            // Update fields if new data is provided
            owner.Name = !string.IsNullOrEmpty(mdlOwner.Name) ? mdlOwner.Name : owner.Name;
            owner.Email = !string.IsNullOrEmpty(mdlOwner.Email) ? mdlOwner.Email : owner.Email;
            owner.PhoneNumper = !string.IsNullOrEmpty(mdlOwner.PhoneNumper) ? mdlOwner.PhoneNumper : owner.PhoneNumper;

            if (!string.IsNullOrEmpty(mdlOwner.Password))
            {
                var hasher = new PasswordHasher<BusinessOwner>();
                owner.PasswordHash = hasher.HashPassword(owner, mdlOwner.Password);
            }

            if (mdlOwner.ImageData != null && mdlOwner.ImageData.Length > 0)
            {
                using var stream = new MemoryStream();
                await mdlOwner.ImageData.CopyToAsync(stream);
                owner.ImageData = stream.ToArray();
            }
            else
            {
                owner.ImageData = oldImageData;
            }

            _appDbContext.BusinessOwners.Update(owner);
            await _appDbContext.SaveChangesAsync();

            return Ok(new
            {
                Message = "Owner data updated successfully!",
                UpdatedData = new
                {
                    owner.Name,
                    owner.Email,
                    owner.PhoneNumper
                }
            });
        }

        #endregion

        // --------- get Owers by name ------------
        // get all Owners from Admin controller
        #region Get Owner By Name
        [HttpGet("GetOwnerByName")]
        public async Task<IActionResult> GetOwnerByName([FromQuery] string? ownerName)
        {

            if (string.IsNullOrWhiteSpace(ownerName))
            {
                var Owners = await _appDbContext.BusinessOwners.ToListAsync();
                return Ok(Owners);
            }
            var ownerByName = await _appDbContext.BusinessOwners.Where(o => o.Name.Trim().ToLower().Contains(ownerName.Trim().ToLower())).ToListAsync();
            if (!ownerByName.Any())
            {
                return NotFound("The destination is not found!");
            }
            return Ok(ownerByName);

        }
        #endregion


        // --------------------------Feedback------------------------
        #region Get Feedbacks By UserId
        [HttpGet("GetFeedbacksByUserId")]
        public async Task<IActionResult> GetFeedbacks([FromQuery] string? contant)
        {

            if (string.IsNullOrWhiteSpace(contant))
            {
                var comment = await _appDbContext.Feedbacks.ToListAsync();
                return Ok(comment);
            }
            var commentByContent = await _appDbContext.Feedbacks.Where(o => o.Content.Trim().ToLower().Contains(contant)).ToListAsync();
            if (!commentByContent.Any())
            {
                return NotFound("The destination is not found!");
            }
            return Ok(commentByContent);

        }
        #endregion

        // Registration Methods 
        #region Admin Regesteration
        [HttpPost("AdminRegistration")]
        [AllowAnonymous]
        
        public async Task<IActionResult> AddNewAdmin([FromForm] MdlAdmin mdlAdmin)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            // Check if email already exists
            var emailExists = await _appDbContext.Admins
                .AnyAsync(a => a.Email == mdlAdmin.Email);
        
            if (emailExists)
                return Conflict("An admin with this email already exists.");
            using var stream = new MemoryStream();
            await mdlAdmin.ImageData.CopyToAsync(stream);

            var admin = new Admin
            {
                Name = mdlAdmin.Name,
                Email = mdlAdmin.Email,
                PhoneNumper = mdlAdmin.PhoneNumper,
                ImageData = stream.ToArray()
            };

            // Hash password using PasswordHasher
            var hasher = new PasswordHasher<Admin>();
            admin.PasswordHash = hasher.HashPassword(admin, mdlAdmin.Password);

            // Save to database
            _appDbContext.Admins.Add(admin);
            await _appDbContext.SaveChangesAsync();

            return Ok("Admin registered successfully.");
        }

        #endregion


        #region Admin Login
        [HttpPost("Login")]
        public async Task<IActionResult> AdminLogin([FromBody] MdlLogin login)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var admin = await _appDbContext.Admins.FirstOrDefaultAsync(a => a.Email == login.Email);
            if (admin == null)
                return NotFound("Admin Email is not valid!");

            var hasher = new PasswordHasher<Admin>();
            var result = hasher.VerifyHashedPassword(admin, admin.PasswordHash, login.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid password!");

            // JWT claims
            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, admin.Email),
                    new Claim(ClaimTypes.NameIdentifier, admin.AdminId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("UserType", "Admin"),
                    new Claim(ClaimTypes.Role, "Admin")
                };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:issuer"],
                audience: _configuration["JWT:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                token = tokenString,
                expiration = token.ValidTo
            });
        }


        #endregion


        #region Logout Admin
        [HttpPost("LogoutAdmin")]
        [Authorize]
        public async Task<IActionResult> LogoutAdmin()
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            var admin = await _appDbContext.Admins.FindAsync(adminId);
            if (admin == null) return Unauthorized();

            // Clear token or perform any logout-specific logic here
            // Example: admin.Token = null;

            _appDbContext.Admins.Update(admin);

            try
            {
                await _appDbContext.SaveChangesAsync();
                return Ok(new { message = "Admin successfully logged out." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Could not log out admin: {ex.Message}");
            }
        }

        #endregion
        //-------------------------------------------------
    }
}
