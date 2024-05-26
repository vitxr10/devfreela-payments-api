
using DevFreela.Payments.API.Models;
using DevFreela.Payments.API.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DevFreela.Payments.API.Consumers
{
    public class ProcessPaymentConsumer : BackgroundService
    {
        private readonly string PAYMENTS_QUEUE = "Payments";
        private readonly string APPROVED_PAYMENTS_QUEUE = "ApprovedPayments";
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ConnectionFactory _factory;
        private readonly IServiceProvider _serviceProvider;
        public ProcessPaymentConsumer(ConnectionFactory factory, IServiceProvider serviceProvider)
        {
            _factory = factory;
            _serviceProvider = serviceProvider;
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: PAYMENTS_QUEUE,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _channel.QueueDeclare(
                queue: APPROVED_PAYMENTS_QUEUE,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (sender, eventArgs) =>
            {
                var byteArray = eventArgs.Body.ToArray();
                var paymentInfoJson = Encoding.UTF8.GetString(byteArray);
                var paymentInfoInputModel = JsonSerializer.Deserialize<PaymentInfoInputModel>(paymentInfoJson);

                ProcessPayment(paymentInfoInputModel);

                var paymentApproved = new PaymentApprovedIntegrationEvent(paymentInfoInputModel.IdProject);
                var paymentApprovedJson = JsonSerializer.Serialize(paymentApproved);
                var paymentApprovedBytes = Encoding.UTF8.GetBytes(paymentApprovedJson);

                _channel.BasicPublish
                (
                   exchange: "",
                   routingKey: APPROVED_PAYMENTS_QUEUE,
                   basicProperties: null,
                   body: paymentApprovedBytes
                );

                _channel.BasicAck(eventArgs.DeliveryTag, false);
            };

            _channel.BasicConsume(PAYMENTS_QUEUE, false, consumer);

            return Task.CompletedTask;
        }

        public void ProcessPayment(PaymentInfoInputModel paymentInfoInputModel)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                paymentService.Process(paymentInfoInputModel);
            }
        }
    }
}
