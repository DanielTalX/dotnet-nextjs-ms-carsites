using AuctionService.DTOs;
using AuctionService.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Data;

public class AuctionRepository : IAuctionRepository
{
    private readonly AuctionDbContext _context;
    private readonly IMapper _mapper;

    public AuctionRepository(AuctionDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void AddAuction(Auction auction)
    {
        _context.Auctions.Add(auction);
    }

    public async Task<AuctionDto> GetAuctionByIdAsync(Guid id)
    {
        /*
        SELECT a."Id", a."AuctionEnd", a."CreatedAt", a."CurrentHighBid", a."ReservePrice", a."Seller", a."SoldAmount", a."Status", a."UpdatedAt", a."Winner",
            i."Id", i."AuctionId", i."Color", i."ImageUrl", i."Make", i."Mileage", i."Model", i."Year"
        FROM "Auctions" AS a
        LEFT JOIN "Items" AS i ON a."Id" = i."AuctionId"
        WHERE a."Id" = @__id_0
        LIMIT 1
        */
        return await _context.Auctions
            .ProjectTo<AuctionDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Auction> GetAuctionEntityById(Guid id)
    {
        return await _context.Auctions
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<AuctionDto>> GetAuctionsAsync(string date)
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

    public void RemoveAuction(Auction auction)
    {
        _context.Auctions.Remove(auction);
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }
}