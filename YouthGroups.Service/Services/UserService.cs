using AutoMapper;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;
using YouthGroups.Service.Common;

namespace YouthGroups.Service.Services;

// Guides management. Only guides are created, updated or deleted here -
// the coordinator account is created once through AuthService.
public class UserService : IUserService
{
    private const string GuideNotFound = "Guide not found.";

    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;

    public UserService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IMapper mapper)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<UserDto>>> GetGuidesAsync(UserQueryDto query, CancellationToken ct)
    {
        var page = await _users.GetGuidesPagedAsync(query.Sort, query.Page, query.PageSize, ct);

        return Result<PagedResult<UserDto>>.Ok(new PagedResult<UserDto>
        {
            Items = _mapper.Map<List<UserDto>>(page.Items),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        });
    }

    public async Task<Result<UserDto>> GetByIdAsync(int id, CurrentUser caller, CancellationToken ct)
    {
        // Checked before the lookup, so a guide cannot learn which ids exist
        if (!caller.IsCoordinator && caller.UserId != id)
            return Result<UserDto>.Forbidden("A guide may only view her own details.");

        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
            return Result<UserDto>.NotFound("User not found.");

        return Result<UserDto>.Ok(_mapper.Map<UserDto>(user));
    }

    public async Task<Result<UserDto>> CreateGuideAsync(UserCreateDto dto, CancellationToken ct)
    {
        var email = Email.Normalize(dto.Email);

        if (await _users.EmailExistsAsync(email, null, ct))
            return Result<UserDto>.Conflict("This email is already in use.");

        if (await _users.NationalIdExistsAsync(dto.NationalId, null, ct))
            return Result<UserDto>.Conflict("This national ID is already in use.");

        var guide = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(dto.InitialPassword), // chosen by the coordinator
            Role = UserRole.Guide,
            FullName = dto.FullName.Trim(),
            Phone = dto.Phone.Trim(),
            NationalId = dto.NationalId,
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(guide, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserDto>.Ok(_mapper.Map<UserDto>(guide));
    }

    public async Task<Result> UpdateAsync(int id, UserUpdateDto dto, CancellationToken ct)
    {
        // Tracked, so changing the properties is enough - SaveChanges writes only what changed
        var guide = await _users.GetForUpdateAsync(id, ct);
        if (guide is null || guide.Role != UserRole.Guide)
            return Result.NotFound(GuideNotFound);

        var email = Email.Normalize(dto.Email);

        // excludeUserId: keeping her own email / national id is not a duplicate
        if (await _users.EmailExistsAsync(email, id, ct))
            return Result.Conflict("This email is already in use.");

        if (await _users.NationalIdExistsAsync(dto.NationalId, id, ct))
            return Result.Conflict("This national ID is already in use.");

        guide.Email = email;
        guide.FullName = dto.FullName.Trim();
        guide.Phone = dto.Phone.Trim();
        guide.NationalId = dto.NationalId;

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var guide = await _users.GetForUpdateAsync(id, ct);
        if (guide is null || guide.Role != UserRole.Guide)
            return Result.NotFound(GuideNotFound);

        // Without this check the database would reject the DELETE (Restrict foreign keys)
        if (await _users.HasLinkedDataAsync(id, ct))
            return Result.Conflict(
                "This guide still has participants, meetings or a taken item. Move or remove them first.");

        _users.Remove(guide);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
