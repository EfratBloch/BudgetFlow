using AutoMapper;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Entities;

namespace BudgetFlow.Service.Mapping;

// Entity -> DTO mappings. UserDto has no PasswordHash, so the hash never leaves the service.
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>();
        CreateMap<Participant, ParticipantDto>();
        CreateMap<Expense, ExpenseDto>();

        // Expects Guide loaded (IBudgetAdditionRepository.GetAllAsync)
        CreateMap<BudgetAddition, BudgetAdditionDto>()
            .ForMember(d => d.GuideName, o => o.MapFrom(a => a.Guide != null ? a.Guide.FullName : string.Empty));

        // HolderName is null while the item is in the store
        CreateMap<Equipment, EquipmentDto>()
            .ForMember(d => d.HolderName, o => o.MapFrom(e => e.Holder != null ? e.Holder.FullName : null));

        // Expects User, Expenses and Participants loaded (IMeetingRepository.GetWithDetailsAsync)
        CreateMap<Meeting, MeetingDto>()
            .ForMember(d => d.ReporterName, o => o.MapFrom(m => m.User.FullName))
            .ForMember(d => d.TotalExpenses, o => o.MapFrom(m => m.Expenses.Sum(e => e.Amount)));
    }
}
