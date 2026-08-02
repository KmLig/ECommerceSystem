namespace OrderService.Domain.Enums
{
    public enum OrderStatusEnum
    {
        Pending = 1,
        Confirmed = 2,
        Cancelled = 3,
        Completed = 4
    }

    public enum PaymentMethodEnum
    {
        COD = 1,
        CreditCard = 2,
        BankTransfer = 3,
        EWallet = 4
    }

    public enum PaymentStatusEnum
    {
        Pending = 1,
        Completed = 2,
        Failed = 3,
        Refunded = 4
    }
}
