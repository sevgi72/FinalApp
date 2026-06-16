using AutoMapper;
using BankApiApp.DTOs.Account;
using BankApiApp.DTOs.Transaction;
using BankApiApp.Models;

namespace BankApiApp.Mappings;

public class MapperProfile : Profile
{
    public MapperProfile()
    {
        CreateMap<Account, AccountDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        CreateMap<Transaction, TransactionDto>()
    .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
    .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
    }
}