using System.Text;
using AutoMapper;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;
using YouthGroups.Core.Settings;

namespace YouthGroups.Service.Services;

public class AuthService : IAuthService
{
    // Same message for an unknown email and a wrong password,
    // so the response does not reveal which emails are registered
    private const string InvalidCredentials = "Invalid email or password.";

    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly JwtSettings _jwt;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IMapper mapper,
        IOptions<JwtSettings> jwtOptions)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _jwt = jwtOptions.Value;
    }

    public async Task<Result<UserDto>> RegisterAsync(RegisterDto dto, CancellationToken ct)
    {
        var email = NormalizeEmail(dto.Email);

        // The system has exactly one coordinator
        if (await _users.CoordinatorExistsAsync(ct))
            return Result<UserDto>.Conflict("A coordinator is already registered.");

        if (await _users.EmailExistsAsync(email, null, ct))
            return Result<UserDto>.Conflict("This email is already in use.");

        if (await _users.NationalIdExistsAsync(dto.NationalId, null, ct))
            return Result<UserDto>.Conflict("This national ID is already in use.");

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(dto.Password), // never the plain password
            Role = UserRole.Coordinator,
            FullName = dto.FullName.Trim(),
            Phone = dto.Phone.Trim(),
            NationalId = dto.NationalId,
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UserDto>.Ok(_mapper.Map<UserDto>(user));
    }

    public async Task<Result<TokenDto>> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(NormalizeEmail(dto.Email), ct);

        if (user is null || !_passwordHasher.Verify(user.PasswordHash, dto.Password))
            return Result<TokenDto>.Validation(InvalidCredentials);

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes);

        return Result<TokenDto>.Ok(new TokenDto
        {
            Token = CreateToken(user, expiresAt),
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            Role = user.Role
        });
    }

    private string CreateToken(User user, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [AppClaimTypes.UserId] = user.Id.ToString(),
                [AppClaimTypes.Role] = user.Role.ToString()
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // Emails are compared case-insensitively: "Sarah@X.com" and "sarah@x.com" are one account
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
