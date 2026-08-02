using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Domain.Repositories
{
    public interface IOrderRepository
    {
        Task<Order?> AddAsync(Order order);
        Task<Order?> GetByIdAsync(Guid orderId);
        Task<bool> ChangeOrderStatusAsync(Guid orderId, OrderStatusEnum status, string changedBy, string reason);
    }

    public interface IMasterDataRepository
    {
        Task<Policy?> GetActiveCancellationPolicyAsync();
        Task<Policy?> GetActiveReturnPolicyAsync();
    }
}
