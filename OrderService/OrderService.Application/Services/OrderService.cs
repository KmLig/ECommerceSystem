using AutoMapper;
using Messaging.Common.Events;
using Microsoft.Extensions.Configuration;
using OrderService.Contracts.Messaging;
using OrderService.Application.DTOs;
using OrderService.Application.Clients;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IUserServiceClient _userServiceClient;
        private readonly IProductServiceClient _productServiceClient;
        private readonly IPaymentServiceClient _paymentServiceClient;
        private readonly INotificationServiceClient _notificationServiceClient;
        private readonly IMapper _mapper;
        private readonly IMasterDataRepository _masterDataRepository;
        private readonly IConfiguration _configuration;
        private readonly IOrderEventPublisher _publisher;

        public OrderService(
            IOrderRepository orderRepository,
            IUserServiceClient userServiceClient,
            IProductServiceClient productServiceClient,
            IPaymentServiceClient paymentServiceClient,
            INotificationServiceClient notificationServiceClient,
            IMasterDataRepository masterDataRepository,
            IMapper mapper,
            IConfiguration configuration,
            IOrderEventPublisher publisher)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _userServiceClient = userServiceClient ?? throw new ArgumentNullException(nameof(userServiceClient));
            _productServiceClient = productServiceClient ?? throw new ArgumentNullException(nameof(productServiceClient));
            _paymentServiceClient = paymentServiceClient ?? throw new ArgumentNullException(nameof(paymentServiceClient));
            _notificationServiceClient = notificationServiceClient ?? throw new ArgumentNullException(nameof(notificationServiceClient));
            _masterDataRepository = masterDataRepository ?? throw new ArgumentNullException(nameof(masterDataRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _publisher = publisher;
        }

        public async Task<OrderResponseDTO> CreateOrderAsync(CreateOrderRequestDTO request, string accessToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Items == null || !request.Items.Any())
                throw new ArgumentException("Order must have at least one item.");

            var user = await _userServiceClient.GetUserByIdAsync(request.UserId, accessToken);
            if (user == null)
                throw new InvalidOperationException("User does not exist.");

            Guid? shippingAddressId = null;
            if (request.ShippingAddressId != null)
            {
                shippingAddressId = request.ShippingAddressId;
            }
            else if (request.ShippingAddress != null)
            {
                request.ShippingAddress.UserId = request.UserId;
                shippingAddressId = await _userServiceClient.SaveOrUpdateAddressAsync(request.ShippingAddress, accessToken);
            }

            Guid? billingAddressId = null;
            if (request.BillingAddressId != null)
            {
                billingAddressId = request.BillingAddressId;
            }
            else if (request.BillingAddress != null)
            {
                request.BillingAddress.UserId = request.UserId;
                billingAddressId = await _userServiceClient.SaveOrUpdateAddressAsync(request.BillingAddress, accessToken);
            }

            if (shippingAddressId == null || billingAddressId == null)
                throw new ArgumentException("Both ShippingAddressId and BillingAddressId must be provided or created.");

            var stockCheckRequests = request.Items.Select(i => new ProductStockVerificationRequestDTO
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList();

            var stockValidation = await _productServiceClient.CheckProductsAvailabilityAsync(stockCheckRequests, accessToken);
            if (stockValidation == null || stockValidation.Any(x => !x.IsValidProduct || !x.IsQuantityAvailable))
                throw new InvalidOperationException("One or more products are invalid or out of stock.");

            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _productServiceClient.GetProductsByIdsAsync(productIds, accessToken);
            if (products == null || products.Count != productIds.Count)
                throw new InvalidOperationException("Failed to retrieve product details for all items.");

            try
            {
                int? cancellationPolicyId = null;
                int? returnPolicyId = null;

                var cancellationPolicy = await _masterDataRepository.GetActiveCancellationPolicyAsync();
                if (cancellationPolicy != null)
                    cancellationPolicyId = cancellationPolicy.Id;

                var returnPolicy = await _masterDataRepository.GetActiveReturnPolicyAsync();
                if (returnPolicy != null)
                    returnPolicyId = returnPolicy.Id;

                var orderId = Guid.NewGuid();
                var orderNumber = GenerateOrderNumberFromGuid(orderId);
                var now = DateTime.UtcNow;

                var initialStatus = request.PaymentMethod == PaymentMethodEnum.COD
                    ? OrderStatusEnum.Confirmed
                    : OrderStatusEnum.Pending;

                var order = new Order
                {
                    Id = orderId,
                    OrderNumber = orderNumber,
                    UserId = request.UserId,
                    ShippingAddressId = shippingAddressId.Value,
                    BillingAddressId = billingAddressId.Value,
                    PaymentMethod = request.PaymentMethod.ToString(),
                    OrderStatusId = (int)initialStatus,
                    CreatedAt = now,
                    OrderDate = now,
                    CancellationPolicyId = cancellationPolicyId,
                    ReturnPolicyId = returnPolicyId,
                    OrderItems = new List<OrderItem>()
                };

                foreach (var item in request.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    order.OrderItems.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        PriceAtPurchase = product.Price,
                        DiscountedPrice = product.DiscountedPrice,
                        Quantity = item.Quantity,
                        ItemStatusId = (int)initialStatus
                    });
                }

                order.SubTotalAmount = Math.Round(order.OrderItems.Sum(i => i.PriceAtPurchase * i.Quantity), 2, MidpointRounding.AwayFromZero);
                order.DiscountAmount = Math.Round(await CalculateDiscountAmountAsync(order.OrderItems), 2, MidpointRounding.AwayFromZero);
                order.TaxAmount = Math.Round(await CalculateTaxAmountAsync(order.SubTotalAmount - order.DiscountAmount), 2, MidpointRounding.AwayFromZero);
                order.ShippingCharges = Math.Round(CalculateShippingCharges(order.SubTotalAmount - order.DiscountAmount), 2, MidpointRounding.AwayFromZero);
                order.TotalAmount = Math.Round(order.SubTotalAmount - order.DiscountAmount + order.TaxAmount + order.ShippingCharges, 2, MidpointRounding.AwayFromZero);

                var addedOrder = await _orderRepository.AddAsync(order);
                if (addedOrder == null)
                    throw new InvalidOperationException("Failed to create order.");

                var paymentRequest = new CreatePaymentRequestDTO
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    Amount = order.TotalAmount,
                    PaymentMethod = request.PaymentMethod
                };
                var paymentResponse = await _paymentServiceClient.InitiatePaymentAsync(paymentRequest, accessToken);
                if (paymentResponse == null)
                    throw new InvalidOperationException("Payment initiation failed.");

                if (request.PaymentMethod == PaymentMethodEnum.COD)
                {
                    #region Event Publishing to RabbitMQ

                    var orderPlacedEvent = new OrderPlacedEvent
                    {
                        OrderId = order.Id,
                        OrderNumber = order.OrderNumber,
                        UserId = order.UserId,
                        CustomerName = user.FullName,
                        CustomerEmail = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        TotalAmount = order.TotalAmount,
                        Items = order.OrderItems.Select(i => new OrderItemLine
                        {
                            ProductId = i.ProductId,
                            Quantity = i.Quantity,
                            UnitPrice = i.PriceAtPurchase
                        }).ToList()
                    };

                    await _publisher.PublishOrderPlacedAsync(orderPlacedEvent, Guid.NewGuid().ToString());

                    #endregion

                    var orderDto = _mapper.Map<OrderResponseDTO>(order);
                    orderDto.OrderStatus = OrderStatusEnum.Confirmed;
                    orderDto.PaymentMethod = PaymentMethodEnum.COD;
                    orderDto.PaymentUrl = null;
                    return orderDto;
                }
                else
                {
                    var orderDto = _mapper.Map<OrderResponseDTO>(order);
                    orderDto.OrderStatus = OrderStatusEnum.Pending;
                    orderDto.PaymentMethod = request.PaymentMethod;
                    orderDto.PaymentUrl = paymentResponse.PaymentUrl;
                    return orderDto;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task<bool> ConfirmOrderAsync(Guid orderId, string accessToken)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            if (order.OrderStatusId != (int)OrderStatusEnum.Pending)
                throw new InvalidOperationException("Order is not in a pending state.");

            var paymentInfo = await _paymentServiceClient.GetPaymentInfoAsync(
                new PaymentInfoRequestDTO { OrderId = orderId }, accessToken);

            if (paymentInfo == null)
                throw new InvalidOperationException("Payment information not found for this order.");

            if (paymentInfo.PaymentStatus != PaymentStatusEnum.Completed)
                throw new InvalidOperationException("Payment is not successful.");

            var user = await _userServiceClient.GetUserByIdAsync(order.UserId, accessToken);
            if (user == null)
                throw new InvalidOperationException("User does not exist.");

            try
            {
                bool statusChanged = await _orderRepository.ChangeOrderStatusAsync(
                    orderId, OrderStatusEnum.Confirmed, "PaymentService", "Payment successful, order confirmed.");

                if (!statusChanged)
                    throw new InvalidOperationException("Failed to update order status.");

                var orderPlacedEvent = new OrderPlacedEvent
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    UserId = order.UserId,
                    CustomerName = user.FullName,
                    CustomerEmail = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    TotalAmount = order.TotalAmount,
                    Items = order.OrderItems.Select(i => new OrderItemLine
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.PriceAtPurchase
                    }).ToList()
                };

                await _publisher.PublishOrderPlacedAsync(orderPlacedEvent, Guid.NewGuid().ToString());

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        private static string GenerateOrderNumberFromGuid(Guid orderId)
        {
            return $"ORD-{orderId.ToString("N")[..12].ToUpperInvariant()}";
        }

        private Task<decimal> CalculateDiscountAmountAsync(IEnumerable<OrderItem> items)
        {
            var discount = items.Sum(i =>
            {
                var unitDiscount = i.PriceAtPurchase - (i.DiscountedPrice ?? i.PriceAtPurchase);
                return unitDiscount > 0 ? unitDiscount * i.Quantity : 0m;
            });
            return Task.FromResult(discount);
        }

        private Task<decimal> CalculateTaxAmountAsync(decimal taxableAmount)
        {
            var taxRate = _configuration.GetValue("OrderSettings:TaxRate", 0.1m);
            return Task.FromResult(taxableAmount * taxRate);
        }

        private decimal CalculateShippingCharges(decimal amountAfterDiscount)
        {
            var freeShippingThreshold = _configuration.GetValue("OrderSettings:FreeShippingThreshold", 500m);
            var flatShipping = _configuration.GetValue("OrderSettings:FlatShippingCharge", 30m);
            return amountAfterDiscount >= freeShippingThreshold ? 0m : flatShipping;
        }
    }
}
