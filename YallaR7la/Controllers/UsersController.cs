using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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
    [AllowAnonymous]
    public class UsersController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _appDbContext;
        public UsersController(AppDbContext appDbContext , UserManager<User> userManager,IConfiguration configuration, IMemoryCache memoryCache)
        {
            _appDbContext = appDbContext;
            _userManager = userManager;
            _configuration = configuration;
            _memoryCache = memoryCache;
        }


        #region GetUserData
        [HttpGet("GetUserData")]
        public async Task<IActionResult> GetUserData(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query parameter is required.");

            var lowerQuery = query.ToLower();

            var users = await _appDbContext.Users
            .Where(u =>
                u.Name.ToLower().Contains(lowerQuery) ||
                u.Email.ToLower().Contains(lowerQuery) ||
                u.City.ToLower().Contains(lowerQuery))
            .ToListAsync();

            if (!users.Any())
                return NotFound("No users matched the search criteria.");

            return Ok(users);
        }


        #endregion


         #region Edit User Info

             [HttpPut("EditUserInfo")]
             [Authorize]
             public async Task<IActionResult> EditUserInfo([FromForm] MdlUser mdlUser)
             {
                 var currutUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                 if (currutUserId == null)
                     return Unauthorized("This user is not authorized!");
            
                 var user = await _appDbContext.Users.FindAsync(currutUserId);
                 if (user == null)
                     return NotFound("User not found!");
            
                
                 user.Name = mdlUser.Name;
                 user.Email = mdlUser.Email;
                 user.City = mdlUser.City;
                 user.Prefrance = mdlUser.Prefrance;
                 user.PhoneNumber = mdlUser.PhoneNumper;
                 user.BirthDate = mdlUser.BirthDate;
            
                 
                 if (mdlUser.ImageData != null && mdlUser.ImageData.Length > 0)
                 {
                     using (var ms = new MemoryStream())
                     {
                         await mdlUser.ImageData.CopyToAsync(ms);
                         user.ImageData = ms.ToArray();
                     }
                 }
                
                 await _appDbContext.SaveChangesAsync();
            
                 return Ok("User profile updated successfully.");
             }
            
            
            
         #endregion


        #region Get User Info
            [HttpGet("GetUserInfo")]
            [Authorize]
            public async Task<IActionResult> GetUserInfo()
            {
                var curruntUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (curruntUserId == null)
                    return NotFound("This User is not found!");
            
                var userInfo = await _appDbContext.Users.Where(o => o.Id == curruntUserId).Select(u => new
                {
                    u.Name,
                    u.Email,
                    u.City,
                    u.Age,
                    u.Prefrance,
                    u.ImageData
                    
                }).FirstOrDefaultAsync();
            
                return Ok(userInfo);
            
            }
        
        #endregion

        // i don't remember why this is found !!!!
        #region Regestriation
        [HttpPost("Regestriation")]
        [AllowAnonymous]
        public async Task<IActionResult> Regestriation([FromForm] MdlUser newUser)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Process the image file (if included)
            using var stream = new MemoryStream();
            await newUser.ImageData.CopyToAsync(stream);

            // Create the User object
            var user = new User
            {
                UserName = newUser.Email,
                Name = newUser.Name,
                Email = newUser.Email,
                PhoneNumper = newUser.PhoneNumper,
                City = newUser.City,
                Prefrance = newUser.Prefrance,
                BirthDate = newUser.BirthDate,
                ImageData = stream.ToArray(),
                UniqueIdImage = Guid.NewGuid() // Automatically generate UniqueIdImage here
            };

            // Create the user
            IdentityResult result = await _userManager.CreateAsync(user, newUser.Password);
            if (result.Succeeded)
            {
                // Generate JWT Token after user creation
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("UserType", "User") // You can modify the UserType according to your role
        };

                // Get roles if any (optional, based on your setup)
                var roles = await _userManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                // Generate the JWT token
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:SecretKey"]));
                var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var token = new JwtSecurityToken(
                    claims: claims,
                    issuer: _configuration["JWT:issuer"],
                    audience: _configuration["JWT:Audience"],
                    expires: DateTime.Now.AddHours(1),
                    signingCredentials: signingCredentials
                );

                var tokenResponse = new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    expiration = token.ValidTo
                };

                // Return the token in the response (don't include UniqueIdImage in the response)
                return Ok(tokenResponse);
            }
            else
            {
                // If there were errors during user creation, add them to ModelState
                foreach (var item in result.Errors)
                {
                    ModelState.AddModelError("", item.Description);
                }
            }

            return BadRequest(ModelState);
        }

        #endregion


        #region Login
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(MdlLogin login)
        {
            if (ModelState.IsValid)
            {
                User user = await _userManager.FindByEmailAsync(login.Email);
                if (user != null)
                {
                    if (await _userManager.CheckPasswordAsync(user, login.Password))
                    {
                        var claims = new List<Claim>();
                        claims.Add(new Claim(ClaimTypes.Email, user.Email));
                        claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
                        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
                        claims.Add(new Claim("UserType", "User"));
                        claims.Add(new Claim(ClaimTypes.Role, "User"));
                        var roles = await _userManager.GetRolesAsync(user);
                        foreach (var role in roles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
                        }

                        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:SecretKey"]));
                        var sc = new SigningCredentials(key,SecurityAlgorithms.HmacSha256);
                        var token = new JwtSecurityToken
                        (
                           claims: claims,
                           issuer: _configuration["JWT:issuer"],
                           audience: _configuration["JWT:Audience"],
                           expires:DateTime.Now.AddHours(1),
                           signingCredentials: sc
                           

                        );
                        var _token = new
                        {
                            token = new JwtSecurityTokenHandler().WriteToken(token),
                            expiration = token.ValidTo
                        };
                        return Ok(_token);

                    }
                    else
                    {
                        return Unauthorized();
                    }

                }
                else
                {
                    ModelState.AddModelError("", "User Email is not valid!");
                }
            }
            return BadRequest(ModelState);
        }
        #endregion




        #region Logout
        [HttpPost("Logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            // Get the current user ID from the JWT claim
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                return Unauthorized();
            }

            // Find the user from the UserManager
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            // If you're storing refresh tokens in AspNetUserTokens, remove the token here
            var removeResult = await _userManager.RemoveAuthenticationTokenAsync(
                user,
                loginProvider: "YallaR7la", 
                tokenName: "RefreshToken" 
            );

            // If removing the refresh token failed, return an error message
            if (!removeResult.Succeeded)
            {
                var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                return StatusCode(500, $"Error revoking refresh token: {errors}");
            }

            // Return success response
            return Ok(new { message = "Successfully logged out, and refresh token revoked." });
        }

        #endregion

        //-------------------------------------------------

        #region Get All Users
        [HttpGet("GetAllUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _appDbContext.Users.ToListAsync();
            return Ok(users);
        }
        #endregion


        #region Add User
        
        //[HttpPost]
        //public async Task<IActionResult> AddUser([FromForm] MdlUser mdlUser)
        //{
        //    using var stream = new MemoryStream();
        //    await mdlUser.ImageData.CopyToAsync(stream);
        //    var user = new User
        //    {
        //        Name = mdlUser.Name,
        //        Email = mdlUser.Email,

        //        PhoneNumper = mdlUser.PhoneNumper,
        //        City = mdlUser.City,
        //        Prefrance = mdlUser.Prefrance,
        //        BirthDate = mdlUser.BirthDate,
        //        ImageData = stream.ToArray(),
        //        UniqueIdImage = Guid.NewGuid()

        //    };
        //    await _appDbContext.AddAsync(user);
        //    await _appDbContext.SaveChangesAsync();
        //    return Ok(user);
        //}
        #endregion
        // --------------- feedback methods-----------------


        #region Add Comment
        [HttpPost("AddComment/{destinationId}")]
        [Authorize(policy: "UserOnly")]
        public async Task<IActionResult> AddComment(string destinationId ,[FromForm]MdlFeedback mdlFeedback)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var comment = new Feedback()
            {
                Content = mdlFeedback.Content,
                Rating = mdlFeedback.Rating,
                UserId = userId,
                DestinationId = destinationId
            };
            await _appDbContext.AddAsync(comment);
            await _appDbContext.SaveChangesAsync();
            return Ok(comment);
        }
        #endregion


        #region Edit Comment

        [HttpPut("EditComment/{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> EditComment(string id, [FromBody] string newContent)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var comment = await _appDbContext.Feedbacks.FindAsync(id);

            if (comment == null || comment.UserId != userId)
                return Forbid("You can only edit your own comments.");

            comment.Content = newContent;
            await _appDbContext.SaveChangesAsync();
            return Ok("Comment updated.");
        }


        #endregion



        #region Delete Comment

        [HttpDelete("DeleteComment/{id}")]
        [Authorize] // Applies to all roles
        public async Task<IActionResult> DeleteComment(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userType = User.FindFirstValue("UserType"); // This assumes you store "Admin", "BusinessOwner", or "User" in a custom claim

            var comment = await _appDbContext.Feedbacks.FindAsync(id);
            if (comment == null)
                return NotFound("Comment not found.");

            // Only the comment owner, admin, or business owner can delete
            if (comment.UserId != userId && userType != "Admin" && userType != "BusinessOwner")
                return Forbid("You are not allowed to delete this comment.");

            _appDbContext.Feedbacks.Remove(comment);
            await _appDbContext.SaveChangesAsync();
            return Ok("Comment deleted.");
        }



        #endregion



        #region Add To Favorites

        [HttpPost("AddToFavorites/{destinationId}")]
        [Authorize] // Ensure only authenticated users can access 
        public async Task<IActionResult> AddToFavorites(string destinationId)
        {

            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User is not authenticated.");
            }

            // Check if the favorite record already exists to avoid duplicates
            var existingFavorite = await _appDbContext.Favorites
                .FirstOrDefaultAsync(f => f.DestinationId == destinationId);
            if (existingFavorite != null)
            {
                return BadRequest("This destination is already in your favorites.");
            }


            var favorite = new Favorite
            {
                UserId = userId,
                FavoritedAt = DateTime.UtcNow,
                DestinationId = destinationId,

            };

            await _appDbContext.Favorites.AddAsync(favorite);
            await _appDbContext.SaveChangesAsync();

            return Ok(favorite);
        }

        #endregion


        #region Delete From Favorites

        [HttpDelete("DeleteFromFavorites/{favoriteId}")]
        [Authorize]
        public async Task<IActionResult> DeleteFromFavorites (string favoriteId)
        {
            var favorite = await _appDbContext.Favorites.FindAsync(favoriteId);
            if (favorite == null)
            {
                return NotFound("Destination is not in favorites!");

            }
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (favorite.UserId !=  currentUserId)
            {
                return Forbid("You do not have Access to remove that from Favorites!");
            }

            _appDbContext.Favorites.Remove(favorite);
            await _appDbContext.SaveChangesAsync();
            return Ok("Destination removed from favorites ");
        }

        #endregion


        #region Reset User Password

        [HttpPatch("ResetPassword/{email}/{newPassword}")]
        public async Task<IActionResult> ResetPassword(string email, string newPassword)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Find user by email
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound("Your email is not found. Please check it!");
            }

            // Generate a password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Reset the password using the token
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Password reset failed", errors });
            }

            return Ok("Your password has been updated successfully.");
        }

        #endregion

    }
}
