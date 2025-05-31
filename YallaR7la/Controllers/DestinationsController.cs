using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YallaR7la.Data;
using YallaR7la.Data.Models;
using YallaR7la.DtoModels;

namespace YallaR7la.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class DestinationsController : ControllerBase
    {
        private readonly AppDbContext _appDbContext;
        public DestinationsController(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        #region Get All Destinations 
        [HttpGet("GetAllDestinations")]
        public async Task<IActionResult> GetAllDestinations()
        {
            var destinations = await _appDbContext.Destinations
                .Where(d => d.IsAvelable == true)
                .Select(d => new
                {
                    d.Name,
                    d.Category,
                    d.Description,
                    AverageRating = d.AverageRating,
                    
                })
                .OrderByDescending(d => d.AverageRating)
                .ToListAsync();

            return Ok(destinations);
        }

        #endregion


        #region Get Destination Details

        [HttpGet("GetDestinationDetails/{destinationId}")]
        public async Task<IActionResult> GetDestinationDetails(string destinationId)
        {
            var destination = await _appDbContext.Destinations
                .Where(d => d.DestinationId == destinationId)
                .Include(d => d.destinationImages)
                .Include(d => d.Feedbacks)
                    .ThenInclude(c => c.User) // Assumes each comment has a User navigation property
                .Select(d => new
                {
                    d.DestinationId,
                    d.Name,
                    d.Description,
                    d.Location,
                    d.Category,
                    d.AvilableNumber,
                    d.Rating,
                    d.AverageRating,
                    d.StartDate,
                    d.EndtDate,
                    d.IsAvelable,
                    d.Discount,
                    d.Cost,
                    d.BusinessOwnerId,
                    Images = d.destinationImages.Select(i => new
                    {
                        i.ImageId,
                        i.ImageData // or i.ImageData if stored as bytes
                    }),
                    Comments = d.Feedbacks.Select(c => new
                    {
                        c.FeedbackId,
                        c.Content,
                        c.dataSubmited,
                        Username = c.User.UserName // or Email
                    })
                })
                .FirstOrDefaultAsync();

            if (destination == null)
                return NotFound("Destination not found.");

            return Ok(destination);
        }


        #endregion

        #region Get By Category

        [HttpGet("GetByCategory")]
        public async Task<IActionResult> GetDestinationsByCategory([FromQuery] string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return BadRequest("Category is required.");

            var destinations = await _appDbContext.Destinations
                .Where(d => d.Category.ToLower() == category.ToLower() && d.IsAvelable == true)
                .Select(d => new
                {
                    d.DestinationId,
                    d.Name,
                    d.Description,
                    d.Category,
                    d.AverageRating,
                    d.Location,
                    d.Cost
                })
                .OrderByDescending(d => d.AverageRating)
                .ToListAsync();

            if (destinations == null || destinations.Count == 0)
                return NotFound($"No destinations found under category: {category}");

            return Ok(destinations);
        }


        #endregion

        #region Add Destination
        [HttpPost("AddDestination")]
        [Authorize(Roles = "BusinessOwner")] // Only Business Owners can add destinations
        public async Task<IActionResult> AddDestination(MdlDestination mdlDestination)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Extract the Owner ID from the token

            if (string.IsNullOrEmpty(ownerId))
                return Unauthorized("Invalid or missing token.");

            var destination = new Destination()
            {
                DestinationId = Guid.NewGuid().ToString(),
                Name = mdlDestination.Name,
                Description = mdlDestination.Description,
                Location = mdlDestination.Location,
                Category = mdlDestination.Category,
                AvilableNumber = mdlDestination.AvilableNumber,
                StartDate = mdlDestination.StartDate,
                EndtDate = mdlDestination.EndtDate,
                Discount = mdlDestination.Discount,
                Cost = mdlDestination.Cost - ((mdlDestination.Discount / 100.0m) * mdlDestination.Cost),
                BusinessOwnerId = ownerId // Assigned from token
            };

            await _appDbContext.Destinations.AddAsync(destination);
            await _appDbContext.SaveChangesAsync();

            return Ok(destination);
        }

        #endregion

        #region AddFeedback

        [HttpPost("AddFeedback/{destinationId}")]
        public async Task<IActionResult> AddFeedback(string destinationId, [FromBody] MdlFeedback model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User not found in token.");

            var destination = await _appDbContext.Destinations
                .Include(d => d.Feedbacks)
                .FirstOrDefaultAsync(d => d.DestinationId == destinationId);

            if (destination == null)
                return NotFound("Destination not found.");

            var feedback = new Feedback
            {
                
                Content = model.Content,
                Rating = model.Rating,
                DestinationId = destinationId,
                UserId = userId,
                dataSubmited = DateTime.UtcNow
            };

            await _appDbContext.Feedbacks.AddAsync(feedback);
            await _appDbContext.SaveChangesAsync();

            // Re-fetch feedbacks after saving to ensure accuracy
            var allRatings = await _appDbContext.Feedbacks
                .Where(f => f.DestinationId == destinationId)
                .Select(f => f.Rating)
                .ToListAsync();

            var average = (int)Math.Round(allRatings.Average());
            destination.AverageRating = average;
            destination.FeedbackCount = allRatings.Count;

            _appDbContext.Destinations.Update(destination);
            await _appDbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Comment and rating submitted successfully.",
                newAverageRating = destination.AverageRating,
                totalComments = destination.FeedbackCount
            });
        }


        #endregion

        #region Book Destination

        [HttpPut("Booking/{destinationId}")]
        public async Task<IActionResult> BookDestination(string destinationId)
        {
            var destination = await _appDbContext.Destinations.FindAsync(destinationId);

            if (destination == null)
            {
                return NotFound(new { message = "Destination not found." });
            }

            if (!destination.IsAvelable)
            {
                return BadRequest(new { message = "This destination is not currently available." });
            }

            if (destination.AvilableNumber <= 0)
            {
                return BadRequest(new { message = "Sorry, the destination is fully booked." });
            }

            destination.AvilableNumber--;

            if (destination.AvilableNumber == 0)
            {
                destination.IsAvelable = false;
            }

            _appDbContext.Destinations.Update(destination);
            await _appDbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Booking successful.",
                RemainingSlots = destination.AvilableNumber
            });
        }



        #endregion


        #region UnBook Destination

        [HttpPut("UnBookDestination/{destinationId}")]
        public async Task<IActionResult> UnBookDestination(string destinationId)
        {
            var destination = await _appDbContext.Destinations.FindAsync(destinationId);

            if (destination == null)
            {
                return NotFound(new { message = "Destination not found." });
            }

            // Increase available slots
            destination.AvilableNumber++;

            if (!destination.IsAvelable && destination.AvilableNumber > 0)
            {
                destination.IsAvelable = true;
            }

            _appDbContext.Destinations.Update(destination);
            await _appDbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Unbooking successful. Slot released.",
                AvailableSlots = destination.AvilableNumber
            });
        }


        #endregion



        #region View Comments 
        [HttpGet("GetCommentsForDestination/{destinationId}")]
        //[Authorize(Roles = "Admin,BusinessOwner")]
        public async Task<IActionResult> GetCommentsForDestination(string destinationId)
        {
            var comments = await _appDbContext.Feedbacks
                .Where(c => c.DestinationId == destinationId)
                .Include(c => c.User)
                .Select(c => new {
                    c.FeedbackId,
                    c.Content,
                    c.dataSubmited,
                    Username = c.User.UserName
                })
                .ToListAsync();

            return Ok(comments);
        }
        #endregion


        #region Get All Comments

        //[HttpGet("GetAllComments/{destinationId}")]
        //[AllowAnonymous]
        //public async Task<IActionResult> GetAllComments(string destinationId)
        //{
        //    var destinationComments = await _appDbContext.Feedbacks.Where(f => f.DestinationId == destinationId).ToListAsync();
        //    if (string.IsNullOrWhiteSpace(destinationId) || destinationComments.Count == 0)
        //    {
        //        return BadRequest("DestinationId is required.");
        //    }


        //    if (destinationComments == null || destinationComments.Count == 0)
        //    {
        //        return NotFound("No comments found for this destination.");
        //    }
        //    return Ok(destinationComments);
        //}

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
                .FirstOrDefaultAsync(f => f.UserId == userId && f.DestinationId == destinationId);
            if (existingFavorite != null)
            {
                return BadRequest("This destination is already in your favorites.");
            }


            var favorite = new Favorite
            {
                UserId = userId,
                DestinationId = destinationId,

            };

            await _appDbContext.Favorites.AddAsync(favorite);
            await _appDbContext.SaveChangesAsync();

            return Ok(favorite);
        }

        #endregion


        #region Search
        [HttpGet("Search")]
        public async Task<IActionResult> SearchDestinations([FromQuery] string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return BadRequest("Search input cannot be empty.");

            // Split input into words and convert to lowercase
            var searchWords = input.ToLower()
                                   .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

            var results = await _appDbContext.Destinations
                .Where(d => searchWords.Any(word =>
                    d.Name.ToLower().Contains(word) ||
                    d.Description.ToLower().Contains(word) ||
                    d.Category.ToLower().Contains(word)))
                .ToListAsync();

            if (!results.Any())
                return NotFound("No destinations match your search.");

            return Ok(results);
        }

        #endregion
    }
}
