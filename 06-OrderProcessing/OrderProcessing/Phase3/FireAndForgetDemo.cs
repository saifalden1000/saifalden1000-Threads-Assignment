using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public sealed class FireAndForgetDemo
{
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public FireAndForgetDemo(
        OrderRepository orderRepository,
        IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    public async Task ProcessOrderAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        await _orderRepository.SaveAsync(order, ct);

#pragma warning disable CS4014
        _emailService.SendConfirmationAsync(order, customer, ct);
#pragma warning restore CS4014

        Console.WriteLine("Order processing finished");
    }

    public async Task ProcessOrderFixedAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        await _orderRepository.SaveAsync(order, ct);

        await _emailService.SendConfirmationAsync(
            order,
            customer,
            ct);

        Console.WriteLine("Order processing finished");
    }
}