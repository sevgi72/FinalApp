using AutoMapper;
using BankApiApp.DTOs.Account;
using BankApiApp.Models;

namespace BankApiApp.Mappings;

public class MapperProfile : Profile
{
    public MapperProfile()
    {
        CreateMap<Account, AccountDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
    }
}