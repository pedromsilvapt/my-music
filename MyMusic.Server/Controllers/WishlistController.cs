using Microsoft.AspNetCore.Mvc;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Wishlist;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("wishlist")]
public class WishlistController(
    ICurrentUser currentUser,
    IWishlistService wishlistService) : ControllerBase
{
    [HttpGet(Name = "ListWishlist")]
    public async Task<ListWishlistResponse> List(
        CancellationToken cancellationToken,
        [FromQuery] long? sourceId = null)
    {
        var items = await wishlistService.ListAsync(currentUser.Id, sourceId, cancellationToken);
        return new ListWishlistResponse
        {
            Items = items.Select(WishlistItem.FromEntity).ToList()
        };
    }

    [HttpPost(Name = "CreateWishlist")]
    public async Task<CreateWishlistResponse> Create(
        [FromBody] CreateWishlistRequest request,
        CancellationToken cancellationToken)
    {
        var item = await wishlistService.CreateAsync(
            currentUser.Id,
            request.SourceId,
            request.Query,
            request.Filter,
            cancellationToken);

        return new CreateWishlistResponse
        {
            Item = WishlistItem.FromEntity(item)
        };
    }

    [HttpPut("{id:long}", Name = "UpdateWishlist")]
    public async Task<UpdateWishlistResponse> Update(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var item = await wishlistService.UpdateHashAsync(id, cancellationToken);
        return new UpdateWishlistResponse
        {
            Item = WishlistItem.FromEntity(item)
        };
    }

    [HttpDelete("{id:long}", Name = "DeleteWishlist")]
    public async Task<ActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        await wishlistService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}