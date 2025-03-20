using Microsoft.AspNetCore.Mvc;
using ShippingServiceAPI.Models;
using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using System.Threading.Tasks;

namespace ShippingServiceAPI.Controllers
{
    [ApiController]
    [Route("api/shipping")]
    public class ShippingController : ControllerBase
    {
        private const string QueueName = "shippingQueue";
        private const string RabbitMqHost = "rabbitmq"; // Docker network name

        [HttpPost]
        public IActionResult ShipOrder([FromBody] OrderDTO order)
        {
            if (order == null)
                return BadRequest("Invalid order data");

            // Omdan OrderDTO til en ShippingRequest
            var shippingRequest = new ShippingRequest
            {
                CustomerName = order.CustomerName,
                PickupAddress = order.PickupAddress,
                DeliveryAddress = order.DeliveryAddress
            };

            // Send til RabbitMQ
            PublishToRabbitMQ(shippingRequest);

            return Ok($"Shipping request created with PackageId: {shippingRequest.PackageId}");
        }

        private async Task PublishToRabbitMQ(ShippingRequest request)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnectionAsync())
            using (var channel = connection.CreateChannel())
            {
                channel.QueueDeclare(queue: "hello",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
                var body = JsonSerializer.SerializeToUtf8Bytes(request);
                channel.BasicPublish(exchange: "",
                routingKey: "hello",
                basicProperties: null,
                body: body);
            }

            // Fix: Create a non-async channel to generate BasicProperties
            using var syncConnection = factory.CreateConnectionAsync();  // Synchronous connection
            using var syncChannel = syncConnection.CreateChannelAsync();  // Synchronous channel
            var properties = syncChannel.CreateBasicProperties();  // Create BasicProperties
            properties.Persistent = false; // Message is not persistent

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: QueueName,
                mandatory: false, // Set to false unless you need guaranteed delivery
                basicProperties: properties,
                body: new ReadOnlyMemory<byte>(body) // Wrap body in ReadOnlyMemory<byte>
            );
        }


    }
}

