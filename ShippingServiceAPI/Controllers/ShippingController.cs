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
        public async Task<IActionResult> ShipOrder([FromBody] OrderDTO order)
        {
            if (order == null)
                return BadRequest("Invalid order data");

            // Convert OrderDTO to ShippingRequest
            var shippingRequest = new ShippingRequest
            {
                CustomerName = order.CustomerName,
                PickupAddress = order.PickupAddress,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryDate = order.DateTime.AddDays(1).ToString("yyyy-MM-dd"),

            };

            // Send to RabbitMQ asynchronously
            await PublishToRabbitMQ(shippingRequest);

            return Ok($"Shipping request created with PackageId: {shippingRequest.PackageId}");
        }

        private async Task PublishToRabbitMQ(ShippingRequest request)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };

            // Open async connection and channel
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var body = JsonSerializer.SerializeToUtf8Bytes(request);

            // Create BasicProperties in an async way
            var properties = new BasicProperties(); // Create an empty BasicProperties object

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: QueueName,
                mandatory: false,
                basicProperties: properties, // Use default properties
                body: new ReadOnlyMemory<byte>(body)
            
           
            );
        }
    }
}
