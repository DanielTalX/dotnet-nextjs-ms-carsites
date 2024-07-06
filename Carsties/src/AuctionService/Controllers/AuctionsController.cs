using AuctionService.Data;
using AuctionService.DTOs;
using AuctionService.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Controllers;

[ApiController]
[Route("api/auctions")]
public class AuctionsController : ControllerBase
{
    private readonly AuctionDbContext _context;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    public AuctionsController(AuctionDbContext context, IMapper mapper,
        IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuctionDto>>> GetAllAuctions(string date)
    {
        var query = _context.Auctions.OrderBy(x => x.Item.Make).AsQueryable();

        if (!string.IsNullOrEmpty(date))
        {
            query = query.Where(x => x.UpdatedAt.CompareTo(DateTime.Parse(date).ToUniversalTime()) > 0);
        }
        
        /*
        SELECT a."Id", a."ReservePrice", a."Seller", a."Winner", COALESCE(a."SoldAmount", 0), COALESCE(a."CurrentHighBid", 0), a."CreatedAt", a."UpdatedAt", a."AuctionEnd", a."Status",
         i."Make", i."Model", i."Year", i."Color", i."Mileage", i."ImageUrl"
        FROM "Auctions" AS a
        LEFT JOIN "Items" AS i ON a."Id" = i."AuctionId"
        ORDER BY i."Make"
        */
        return await query.ProjectTo<AuctionDto>(_mapper.ConfigurationProvider).ToListAsync();
    }

    [HttpGet("unused")]
    public async Task<ActionResult<List<AuctionDto>>> GetAllAuctionsOptions()
    {
        /*
        SELECT a."Id", a."AuctionEnd", a."CreatedAt", a."CurrentHighBid", a."ReservePrice", a."Seller", a."SoldAmount", a."Status", a."UpdatedAt", a."Winner",
            i."Id", i."AuctionId", i."Color", i."ImageUrl", i."Make", i."Mileage", i."Model", i."Year"
        FROM "Auctions" AS a
        LEFT JOIN "Items" AS i ON a."Id" = i."AuctionId"
        ORDER BY i."Make"
        */
        var auctions = await _context.Auctions
            .Include(x => x.Item)
            .OrderBy(x => x.Item.Make)
            .ToListAsync();

        return _mapper.Map<List<AuctionDto>>(auctions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AuctionDto>> GetAuctionById(Guid id)
    {
        /*
        SELECT a."Id", a."AuctionEnd", a."CreatedAt", a."CurrentHighBid", a."ReservePrice", a."Seller", a."SoldAmount", a."Status", a."UpdatedAt", a."Winner",
            i."Id", i."AuctionId", i."Color", i."ImageUrl", i."Make", i."Mileage", i."Model", i."Year"
        FROM "Auctions" AS a
        LEFT JOIN "Items" AS i ON a."Id" = i."AuctionId"
        WHERE a."Id" = @__id_0
        LIMIT 1
        */
        var auction = await _context.Auctions
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (auction == null) return NotFound();

        return _mapper.Map<AuctionDto>(auction);
    }

    [HttpPost]
    public async Task<ActionResult<AuctionDto>> CreateAuction(CreateAuctionDto auctionDto)
    {
        var auction = _mapper.Map<Auction>(auctionDto);
        // TODO: add current user as seller
        auction.Seller = "test";
        // save in memory
        _context.Auctions.Add(auction);

        var newAuction = _mapper.Map<AuctionDto>(auction);

        await _publishEndpoint.Publish(_mapper.Map<AuctionCreated>(newAuction));

        // save in db
        var result = await _context.SaveChangesAsync() > 0;

        if (!result) return BadRequest("Could not save changes to the DB");

        return CreatedAtAction(nameof(GetAuctionById), 
            new {auction.Id}, newAuction);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateAuction(Guid id, UpdateAuctionDto updateAuctionDto)
    {
        var auction = await _context.Auctions.Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (auction == null) return NotFound();

        // TODO: check seller == username

        auction.Item.Make = updateAuctionDto.Make ?? auction.Item.Make;
        auction.Item.Model = updateAuctionDto.Model ?? auction.Item.Model;
        auction.Item.Color = updateAuctionDto.Color ?? auction.Item.Color;
        auction.Item.Mileage = updateAuctionDto.Mileage ?? auction.Item.Mileage;
        auction.Item.Year = updateAuctionDto.Year ?? auction.Item.Year;

        await _publishEndpoint.Publish(_mapper.Map<AuctionUpdated>(auction));

        var result = await _context.SaveChangesAsync() > 0;

        if (result) return Ok();

        return BadRequest("Problem saving changes");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAuction(Guid id)
    {
        var auction = await _context.Auctions.FindAsync(id);

        if (auction == null) return NotFound();

        // TODO: check seller == username

        _context.Auctions.Remove(auction);

        await _publishEndpoint.Publish<AuctionDeleted>(new { Id = auction.Id.ToString() });

        var result = await _context.SaveChangesAsync() > 0;

        if (!result) return BadRequest("Could not update DB");

        return Ok();
    }

}