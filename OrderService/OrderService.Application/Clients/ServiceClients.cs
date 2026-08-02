using OrderService.Application.DTOs;

namespace OrderService.Application.Clients
{
    public interface IUserServiceClient
    {
        Task<UserDTO?> GetUserByIdAsync(Guid userId, string accessToken);
        Task<Guid> SaveOrUpdateAddressAsync(AddressDTO address, string accessToken);
    }

    public interface IProductServiceClient
    {
        Task<List<ProductStockVerificationResponseDTO>> CheckProductsAvailabilityAsync(
            List<ProductStockVerificationRequestDTO> requests, string accessToken);
        Task<List<ProductDTO>> GetProductsByIdsAsync(List<Guid> productIds, string accessToken);
    }

    public interface IPaymentServiceClient
    {
        Task<CreatePaymentResponseDTO?> InitiatePaymentAsync(CreatePaymentRequestDTO request, string accessToken);
        Task<PaymentInfoResponseDTO?> GetPaymentInfoAsync(PaymentInfoRequestDTO request, string accessToken);
    }

    public interface INotificationServiceClient
    {
    }
}
