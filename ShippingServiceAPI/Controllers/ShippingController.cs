using Microsoft.AspNetCore.Mvc;
using ShippingServiceAPI.Models;
using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using System.Threading.Tasks;
using System.Formats.Asn1;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ShippingServiceAPI.Controllers
{
    [ApiController]
    [Route("api/shipping")]
    public class ShippingController : ControllerBase
    {
        private const string QueueName = "shippingQueue"; // Name of the RabbitMQ queue
        private const string RabbitMqHost = "rabbitmq"; // Docker network name
        private const string CsvFilePath = "/app/data/shippingRequests.csv"; // Sti til volumen

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
            // Læs RabbitMQ host fra miljøvariabler (Docker Compose sætter denne automatisk)
            var rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            var factory = new ConnectionFactory() { HostName = rabbitMqHost };

            // Opret forbindelse til RabbitMQ
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

        [HttpGet]
        [Route("deliveryplan")]
        public IActionResult GetDeliveryPlanForToday()
        {
            try
            {
                if (!System.IO.File.Exists(CsvFilePath))
                {
                    return NotFound("CSV file not found.");
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                using var reader = new StreamReader(CsvFilePath);
                using var csv = new CsvReader(reader, config);

                var records = csv.GetRecords<Ship>().ToList();

                return Ok(records);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.InnerException.Message);
            }
        }


    }

    public class Ship
    {
        public string CustomerName { get; set; }
        public string PickupAddress { get; set; }
        public string PackageId { get; set; } 
        public string DeliveryAddress { get; set; }

        public string Date { get; set; }
    }

}
