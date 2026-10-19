using AutoMapper;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;

namespace YouthGroups.Service.Mapping;

// Entity -> DTO mappings. UserDto has no PasswordHash, so the hash never leaves the service.
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>();
        CreateMap<Participant, ParticipantDto>();
    }
}
