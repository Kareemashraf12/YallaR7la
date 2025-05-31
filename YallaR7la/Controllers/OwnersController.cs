using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    //[Authorize(policy: "OwnerOnly")]
    public class OwnersController : ControllerBase
    {
        private readonly AppDbContext _appDbContext;
        //private readonly UserManager<BusinessOwner> _ownerManager;
        private readonly IConfiguration _configuration;

        public OwnersController(AppDbContext appDbContext ,IConfiguration configuration)
        {
            _appDbContext = appDbContext;
            //_ownerManager = ownerManager;
            _configuration = configuration;
        }

        // handel destination from owner controller

        #region Get All Destinations 
        [HttpGet("GetAllDestinations")]
        public async Task<IActionResult> GetAllDestinations()
        {
            var destinations = await _appDbContext.Destinations
                .Include(d => d.destinationImages)
                .Select(d => new
                {
                    d.DestinationId,
                    d.Name,
                    d.Description,
                    d.Location,
                    d.Category,
                    d.AvilableNumber,
                    d.StartDate,
                    d.EndtDate,
                    d.Discount,
                    d.Cost,
                    d.BusinessOwnerId,
                    Images = d.destinationImages.Select(img => new
                    {
                        img.ImageId,
                        ImageBase64 = Convert.ToBase64String(img.ImageData)
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new
            {
                Count = destinations.Count,
                Destinations = destinations
            });
        }

        #endregion

         #region Get Owner Info
             [HttpGet("GetOwnerInfo")]
             [Authorize]
             public async Task<IActionResult> GetOwnerInfo ()
             {
                 var currentOwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                 if (currentOwnerId == null)
                     return NotFound("There is error in your token !");
                 var ownerinfo = await _appDbContext.BusinessOwners.Where(o => o.BusinessOwnerId == currentOwnerId).Select(o => new
                 {
                     o.Name,
                     o.Email,
                     o.PhoneNumper,
                     o.ImageData
                 }).FirstOrDefaultAsync();
            
                 if (ownerinfo == null)
                     return NotFound("This Owner is not found!");
            
                 return Ok(ownerinfo);
             }
        
         #endregion


            #region Update Owner 
            
                [HttpPut("UpdateOwner/{ownerId}")]
                [Authorize]
                public async Task<IActionResult> UpdateOwner( [FromForm] MdlOwner mdlOwner)
                {
                    var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var owner = await _appDbContext.BusinessOwners.FirstOrDefaultAsync(o => o.BusinessOwnerId == ownerId);
                    if (owner == null)
                    {
                        return NotFound(new { Message = "This Owner was not found!" });
                    }
                
                    var existingData = new
                    {
                        owner.Name,
                        owner.Email,
                        owner.PhoneNumper,
                        owner.ImageData
                        
                    };
                
                    if (mdlOwner.Name == null && mdlOwner.Email == null && mdlOwner.PhoneNumper == null && mdlOwner.ImageData == null)
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
                            owner.PhoneNumper,
                            owner.ImageData
                        }
                    });
                }
            
            #endregion
         
        #region Get DestinationByOwnerId

        [HttpGet("GetDestinationsWithImagesByOwner/{ownerId}")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> GetDestinationsWithImagesByOwner(string ownerId)
        {
            var currentOwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentOwnerId != ownerId)
            {
                return Forbid("You are not authorized to view these destinations.");
            }

            var destinationsWithImages = await _appDbContext.Destinations
                .Where(d => d.BusinessOwnerId == ownerId)
                .Include(d => d.destinationImages) // <-- includes the related images
                .ToListAsync();

            if (destinationsWithImages == null || !destinationsWithImages.Any())
            {
                return NotFound("No destinations found for this owner.");
            }

            //  remove image binary data if needed to avoid large payloads
            var result = destinationsWithImages.Select(dest => new
            {
                dest.Name,
                dest.Description,
                dest.Location,
                dest.Category,
                dest.StartDate,
                dest.EndtDate,
                dest.Discount,
                dest.Cost,
                dest.AvilableNumber,
                Images = dest.destinationImages.Select(img => new
                {
                    ImageId = img.ImageId,
                    ImageBase64 = Convert.ToBase64String(img.ImageData) //  return image data
                })
            });

            return Ok(result);
        }


        #endregion


        #region Get Destination By Id & if id = null get all Dest
        [HttpGet("GetDestinationById ")]
        public async Task<IActionResult> GetDestination([FromQuery]string? destinationId)
        {
            
            if (destinationId == null)
            {
                var destinations = await _appDbContext.Destinations.Include(d => d.destinationImages).Select(d => new
                {
                    d.DestinationId,
                    d.Name,
                    d.Description,
                    d.Location,
                    d.Category,
                    d.AvilableNumber,
                    d.StartDate,
                    d.EndtDate,
                    d.Discount,
                    d.Cost,
                    d.BusinessOwnerId,
                    Images = d.destinationImages.Select(img => new
                    {
                        img.ImageId,
                        ImageBase64 = Convert.ToBase64String(img.ImageData)
                    }).ToList()
                }).ToListAsync();
                return Ok(destinations);
            }
            var destinationById = await _appDbContext.Destinations.FindAsync(destinationId);
            if (destinationById == null)
            {
                return NotFound("The destination is not found!");
            }
            return Ok(destinationById);

        }

        #endregion



        #region Add Destination
        [HttpPost("AddDestination")]
        public async Task<IActionResult> AddDestination(MdlDestination mdlDestination)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var destination = new Destination()
            {
                Name = mdlDestination.Name,
                Description = mdlDestination.Description,
                Location = mdlDestination.Location,
                Category = mdlDestination.Category,
                AvilableNumber = mdlDestination.AvilableNumber,
         
                StartDate = mdlDestination.StartDate,
                EndtDate = mdlDestination.EndtDate,
                Discount = mdlDestination.Discount,
                Cost = mdlDestination.Cost - ((mdlDestination.Discount * 100) * mdlDestination.Cost),
                BusinessOwnerId = ownerId
            };
            await _appDbContext.AddAsync(destination);
            await _appDbContext.SaveChangesAsync();
            return Ok(destination);
        }
        #endregion


        #region Add Destination Image

        [HttpPost("AddDestinationImage/{destinationId}/{ownerId}")]
        [Authorize(policy: "OwnerOnly")]
        public async Task<IActionResult> AddDestinationImage (string ownerId,string destinationId,MdlDistanationImages mdlDistanationImages)
        {
            var currentOwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (ownerId != currentOwnerId )
            {
                return Forbid(" You have not access to do that!");
            }
            if (mdlDistanationImages.ImageData == null || mdlDistanationImages.ImageData.Length == 0)
            {
                return BadRequest("No images uploaded.");
            }
            var destinationImages = new List<DestinationImages>();

            foreach (var file in mdlDistanationImages.ImageData)
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                var destinationImage = new DestinationImages
                {
                    ImageData = stream.ToArray(),
                    DestinationId = destinationId
                };
                destinationImages.Add(destinationImage);
                await _appDbContext.AddAsync(destinationImage);
            }

            await _appDbContext.SaveChangesAsync();
            return Ok(destinationImages);
        }

        #endregion



        #region AddDestinationAndImages

        [HttpPost("AddDestinationWithImages")]
        [Authorize(policy: "OwnerOnly")]
        public async Task<IActionResult> AddDestinationWithImages([FromForm] MdlDestinationWithImages model)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(ownerId))
                return Unauthorized("Owner ID not found in token.");

            string destinationId = Guid.NewGuid().ToString();

            var destination = new Destination()
            {
                DestinationId = destinationId,
                Name = model.Name,
                Description = model.Description,
                Location = model.Location,
                Category = model.Category,
                AvilableNumber = model.AvilableNumber,
                StartDate = model.StartDate,
                EndtDate = model.EndtDate,
                Discount = model.Discount,
                IsAvelable = true,
                Cost = model.Cost - ((model.Discount / 100) * model.Cost),
                BusinessOwnerId = ownerId
            };

            await _appDbContext.Destinations.AddAsync(destination);

            var destinationImages = new List<DestinationImages>();

            foreach (var file in model.ImageData)
            {
                if (file != null && file.Length > 0)
                {
                    using var stream = new MemoryStream();
                    await file.CopyToAsync(stream);

                    var image = new DestinationImages
                    {
                        ImageId = Guid.NewGuid().ToString(),
                        DestinationId = destinationId,
                        ImageData = stream.ToArray()
                    };

                    destinationImages.Add(image);
                }
            }

            await _appDbContext.DestinationImages.AddRangeAsync(destinationImages);
            await _appDbContext.SaveChangesAsync();

            // Return destination with included images (Base64 encoded)
            var addedDestination = await _appDbContext.Destinations
                .Where(d => d.DestinationId == destinationId)
                .Select(d => new
                {
                    d.DestinationId,
                    d.Name,
                    d.Description,
                    d.Location,
                    d.Category,
                    d.AvilableNumber,
                    d.StartDate,
                    d.EndtDate,
                    d.Discount,
                    d.Cost,
                    d.BusinessOwnerId,
                    Images = _appDbContext.DestinationImages
                        .Where(img => img.DestinationId == d.DestinationId)
                        .Select(img => new
                        {
                            img.ImageId,
                            ImageBase64 = Convert.ToBase64String(img.ImageData)
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                Message = "Destination and images added successfully",
                Destination = addedDestination
            });
        }



        #endregion


        #region UpdateDestinationWithImages

        [HttpPut("UpdateDestinationWithImages/{destinationId}")]
        [Authorize(policy: "OwnerOnly")]
        public async Task<IActionResult> UpdateDestinationWithImages(string destinationId, [FromForm] MdlDestinationWithImages model)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(ownerId))
                return Unauthorized("Owner ID not found in token.");

            var destination = await _appDbContext.Destinations
                .FirstOrDefaultAsync(d => d.DestinationId == destinationId && d.BusinessOwnerId == ownerId);

            if (destination == null)
                return NotFound("Destination not found or you are not authorized to edit it.");

            // Update destination fields
            destination.Name = model.Name;
            destination.Description = model.Description;
            destination.Location = model.Location;
            destination.Category = model.Category;
            destination.AvilableNumber = model.AvilableNumber;
            destination.StartDate = model.StartDate;
            destination.EndtDate = model.EndtDate;
            destination.Discount = model.Discount;
            destination.Cost = model.Cost - ((model.Discount / 100) * model.Cost);

            // Check if new images are uploaded
            bool hasNewImages = model.ImageData != null && model.ImageData.Any(file => file != null && file.Length > 0);

            if (hasNewImages)
            {
                var oldImages = await _appDbContext.DestinationImages
                    .Where(img => img.DestinationId == destinationId)
                    .ToListAsync();
                _appDbContext.DestinationImages.RemoveRange(oldImages);

                var destinationImages = new List<DestinationImages>();
                foreach (var file in model.ImageData!)
                {
                    if (file != null && file.Length > 0)
                    {
                        using var stream = new MemoryStream();
                        await file.CopyToAsync(stream);

                        destinationImages.Add(new DestinationImages
                        {
                            ImageId = Guid.NewGuid().ToString(),
                            DestinationId = destinationId,
                            ImageData = stream.ToArray()
                        });
                    }
                }
                await _appDbContext.DestinationImages.AddRangeAsync(destinationImages);
            }

            await _appDbContext.SaveChangesAsync();

            // Return updated destination with images
            var updatedDestination = await _appDbContext.Destinations
                .Where(d => d.DestinationId == destinationId)
                .Select(d => new
                {
                    d.DestinationId,
                    d.Name,
                    d.Description,
                    d.Location,
                    d.Category,
                    d.AvilableNumber,
                    d.StartDate,
                    d.EndtDate,
                    d.Discount,
                    d.Cost,
                    d.BusinessOwnerId,
                    Images = _appDbContext.DestinationImages
                        .Where(img => img.DestinationId == d.DestinationId)
                        .Select(img => new
                        {
                            img.ImageId,
                            ImageBase64 = Convert.ToBase64String(img.ImageData)
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                Message = hasNewImages ? "Destination and images updated successfully." : "Destination updated. Existing images kept.",
                Destination = updatedDestination
            });
        }

        #endregion



        #region Delete all Destination Image

        //[HttpDelete("DeleteAllDestinationImage/{ownerId}/{destintaionId}")]
        //public async Task<IActionResult> DeleteAllDestinationImage(string destinationId)
        //{
        //    var currentOwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    if (currentOwnerId == null)
        //    {
        //        return Forbid("You are not authorized to delete images for this destination.");
        //    }

        //    var destination = await _appDbContext.Destinations
        //    .Include(d => d.destinationImages)
        //    .FirstOrDefaultAsync(d => d.DestinationId.ToString() == destinationId);
        //    if (destination == null)
        //    {
        //        return NotFound("No destination found .");
        //    }
        //    if (destination.destinationImages == null || !destination.destinationImages.Any())
        //    {
        //        return NotFound("No images found for this destination.");
        //    }
        //    _appDbContext.DestinationImages.RemoveRange(destination.destinationImages);
        //    await _appDbContext.SaveChangesAsync();
        //    return Ok("All images for the destination have been deleted.");
        //}

        #endregion



        #region Delete One Image

        [HttpDelete("DeleteOneImage/{imageId}")]
        [Authorize(policy: "OwnerOnly")]
        public async Task<IActionResult> DeleteOneImage(string imageId)
        {
            // Get current logged-in owner's ID from the JWT token
            var currentOwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if destination exists and belongs to current owner
            var destination = await _appDbContext.Destinations
                .FirstOrDefaultAsync(d => d.BusinessOwnerId == currentOwnerId);

            if (destination == null)
            {
                return Forbid("You are not authorized to delete images for this destination.");
            }

            // Find the image by destinationId and imageId
            var image = await _appDbContext.DestinationImages
                .FirstOrDefaultAsync(img => img.ImageId == imageId);

            if (image == null)
            {
                return NotFound("Image not found.");
            }

            // Delete image
            _appDbContext.DestinationImages.Remove(image);
            await _appDbContext.SaveChangesAsync();

            return Ok(new
            {
                Message = "The image has been deleted successfully.",
                DeletedImageId = imageId
            });
        }


        #endregion


        #region Update Destination

        //[HttpPut("UpdateDestination/{destinationId}")]
        //public async Task<IActionResult> UpdateDestination(string destinationId, [FromForm] MdlDestination mdlDestination)
        //{
        //    // Retrieve the destination to update
        //    var destination = await _appDbContext.Destinations.FindAsync(destinationId);
        //    if (destination == null)
        //    {
        //        return NotFound("Destination not found.");
        //    }
            
            
        //    var currentOwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    if (destination.BusinessOwnerId != currentOwnerId)
        //    {
        //        return Forbid("You are not authorized to update this destination.");
        //    }

        //    var destinationToUpdate = new Destination()
        //    {
        //        Name = mdlDestination.Name,
        //        Description = mdlDestination.Description,
        //        Location = mdlDestination.Location,
        //        Category = mdlDestination.Category,
        //        AvilableNumber = mdlDestination.AvilableNumber,
            
        //        StartDate = mdlDestination.StartDate,
        //        EndtDate = mdlDestination.EndtDate,
               
        //        Discount = mdlDestination.Discount,
        //        Cost = mdlDestination.Cost - ((mdlDestination.Discount * 100) * mdlDestination.Cost),
        //    };
           

        //    _appDbContext.Destinations.Update(destinationToUpdate);
        //    await _appDbContext.SaveChangesAsync();
        //    return Ok(destination);
        //}

        #endregion


        #region Delete Destination

        [HttpDelete("DeleteDestination/{destinationId}")]
        public async Task<IActionResult> DeleteDestination(string destinationId)
        {
            var destination = await _appDbContext.Destinations.FindAsync(destinationId);
            if (destination == null)
            {
                return NotFound("The Destination is not found!");
            }
             
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (destination.BusinessOwnerId != ownerId)
            {
                return Forbid("You can not delete this destination!");
            }
            _appDbContext.Destinations.Remove(destination);
            await _appDbContext.SaveChangesAsync();
            return Ok("The destination is removed!");
        }


        #endregion

        // ------------------------- login -----------------------------
        #region Login As Owner
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] MdlLogin login)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Find the business owner by email
            var owner = await _appDbContext.BusinessOwners.FirstOrDefaultAsync(o => o.Email == login.Email);
            if (owner == null)
                return NotFound("Owner Email is not valid!");

            // Verify the password
            var hasher = new PasswordHasher<BusinessOwner>();
            var result = hasher.VerifyHashedPassword(owner, owner.PasswordHash, login.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid password!");

            // Create claims for the token
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, owner.Email),
        new Claim(ClaimTypes.NameIdentifier, owner.BusinessOwnerId.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim("UserType", "Owner"),
        new Claim(ClaimTypes.Role, "Owner") // Assign role as "Owner"
    };

            // Generate the token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:issuer"],
                audience: _configuration["JWT:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
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


        #region LogoutOwner

        [HttpPost("LogoutOwner")]
        [Authorize]
        public async Task<IActionResult> LogoutOwner()
        {
            var ownerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(ownerId)) return Unauthorized();

            var owner = await _appDbContext.BusinessOwners.FindAsync(ownerId);
            if (owner == null) return Unauthorized();

            //owner.Token = null;
            _appDbContext.BusinessOwners.Update(owner);

            try
            {
                await _appDbContext.SaveChangesAsync();
                return Ok(new { message = "Owner successfully logged out." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Could not log out Owner: {ex.Message}");
            }
        }

        #endregion


        #region Reset Owner Password

        [Authorize(Roles = "BusinessOwner")]
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPasswordForOwner([FromBody] MdlResetPassword model)
        {
            var ownerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(ownerId)) return Unauthorized();

            var owner = await _appDbContext.BusinessOwners.FindAsync(ownerId);
            if (owner == null) return NotFound("Business Owner not found!");

            var hasher = new PasswordHasher<BusinessOwner>();
            var result = hasher.VerifyHashedPassword(owner, owner.PasswordHash, model.OldPassword);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Old password is incorrect!");

            owner.PasswordHash = hasher.HashPassword(owner, model.NewPassword);
           
            try
            {
                _appDbContext.BusinessOwners.Update(owner);
                await _appDbContext.SaveChangesAsync();
                return Ok("Password has been reset successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        #endregion
        //-------------------------------------------------

    }
}
