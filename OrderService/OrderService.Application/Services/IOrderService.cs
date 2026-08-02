using OrderService.Application.DTOs;

namespace OrderService.Application.Services
{
    public interface IOrderService
    {
        Task<OrderResponseDTO> CreateOrderAsync(CreateOrderRequestDTO request, string accessToken);
        Task<bool> ConfirmOrderAsync(Guid orderId, string accessToken);
    }
}
