using Accounts.Application.Accounts.DTOs;
using Accounts.Domain.Accounts.Entities;
using Mapster;

namespace Accounts.Application.Common.Mappings
{
    public class MappingRegister : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Account, AccountDto>();
        }
    }
}
