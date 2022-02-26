using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.Theme;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IBookThemeRepository
{
    Task<IEnumerable<SiteThemeDto>> GetThemeDtosForUser(int userId);
}

public class BookThemeRepository : IBookThemeRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public BookThemeRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }


    public async Task<IEnumerable<SiteThemeDto>> GetThemeDtosForUser(int userId)
    {
        // TODO: Provide proper implementation
        return await _context.SiteTheme
            .ProjectTo<SiteThemeDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}
