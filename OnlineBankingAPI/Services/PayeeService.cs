using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Repositories;

namespace OnlineBankingAPI.Services;

/// <summary>Payees are read-only in the prototype (open question: add create and delete later?).</summary>
public class PayeeService(IPayeeRepository payees)
{
    public async Task<PayeeListDto> GetPayeesAsync(Guid customerId, CancellationToken ct)
    {
        var list = await payees.GetPayeesForCustomerAsync(customerId, ct);
        return new PayeeListDto(list
            .Select(p => new PayeeDto(p.Id, p.Nickname, p.Name, p.BankName, p.MaskedAccountNumber, p.Category))
            .ToList());
    }
}
