using OrderService.Domain.Enums;

namespace OrderService.Application.DTOs
{
    public class CreateOrderRequestDTO
    {
        public Guid UserId { get; set; }
        public Guid? ShippingAddressId { get; set; }
        public Guid? BillingAddressId { get; set; }
        public AddressDTO? ShippingAddress { get; set; }
        public AddressDTO? BillingAddress { get; set; }
        public PaymentMethodEnum PaymentMethod { get; set; }
        public List<CreateOrderItemDTO> Items { get; set; } = new();
    }

    public class CreateOrderItemDTO
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class AddressDTO
    {
        public Guid UserId { get; set; }
        public string AddressLine1 { get; set; } = null!;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string PostalCode { get; set; } = null!;
        public string Country { get; set; } = null!;
    }

    public class OrderResponseDTO
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = null!;
        public OrderStatusEnum OrderStatus { get; set; }
        public PaymentMethodEnum PaymentMethod { get; set; }
        public string? PaymentUrl { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ProductStockVerificationRequestDTO
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class ProductStockVerificationResponseDTO
    {
        public Guid ProductId { get; set; }
        public bool IsValidProduct { get; set; }
        public bool IsQuantityAvailable { get; set; }
    }

    public class ProductDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
    }

    public class CreatePaymentRequestDTO
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethodEnum PaymentMethod { get; set; }
    }

    public class CreatePaymentResponseDTO
    {
        public string? PaymentUrl { get; set; }
    }

    public class PaymentInfoRequestDTO
    {
        public Guid OrderId { get; set; }
    }

    public class PaymentInfoResponseDTO
    {
        public PaymentStatusEnum PaymentStatus { get; set; }
    }

    public class UserDTO
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
    }
}
