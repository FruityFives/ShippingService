using Microsoft.AspNetCore.Mvc;
using ShippingServiceAPI.Models;
using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using System.Threading.Tasks;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using System.Diagnostics;

namespace ShippingServiceAPI.Controllers
{
    [ApiController]
    [Route("api/shipping")]
    public class ShippingController : ControllerBase
    {
        private const string QueueName = "shippingQueue";
        private const string RabbitMqHost = "rabbitmq";
        private const string CsvFilePath = "/app/data/shippingRequests.csv";

        private readonly ILogger<ShippingController> _logger;

        public ShippingController(ILogger<ShippingController> logger)
        {
            _logger = logger;

            try
            {
                var hostName = System.Net.Dns.GetHostName();
                var ips = System.Net.Dns.GetHostAddresses(hostName);
                var ipaddr = ips.First().MapToIPv4().ToString();
                _logger.LogInformation("ShippingService responding from {ip}", ipaddr);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve server IP address");
            }
        }

        [HttpGet("version")]
        public async Task<Dictionary<string, string>> GetVersion()
        {
            var properties = new Dictionary<string, string>();
            var assembly = typeof(Program).Assembly;

            properties.Add("service", "HaaV Shipping Service");

            var ver = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
            properties.Add("version", ver!);

            try
            {
                var hostName = System.Net.Dns.GetHostName();
                var ips = await System.Net.Dns.GetHostAddressesAsync(hostName);
                var ipa = ips.First().MapToIPv4().ToString();
                properties.Add("hosted-at-address", ipa);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not resolve IP address");
                properties.Add("hosted-at-address", "Could not resolve IP-address");
            }

            return properties;
        }

        [HttpPost]
        public async Task<IActionResult> ShipOrder([FromBody] OrderDTO order)
        {
            if (order == null)
            {
                _logger.LogWarning("Received null order in ShipOrder");
                return BadRequest("Invalid order data");
            }

            _logger.LogInformation("Received order from {CustomerName} to {DeliveryAddress}",
                order.CustomerName, order.DeliveryAddress);

            var shippingRequest = new ShippingRequest
            {
                CustomerName = order.CustomerName,
                PickupAddress = order.PickupAddress,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryDate = order.DateTime.AddDays(1).ToString("yyyy-MM-dd")
            };

            _logger.LogDebug("Converted order to shippingRequest: {shippingRequest}", JsonSerializer.Serialize(shippingRequest));

            await PublishToRabbitMQ(shippingRequest);

            _logger.LogInformation("Shipping request for {CustomerName} published to RabbitMQ", shippingRequest.CustomerName);

            return Ok($"Shipping request created with PackageId: {shippingRequest.PackageId}");
        }

        private async Task PublishToRabbitMQ(ShippingRequest request)
        {
            var rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
            _logger.LogInformation("Using RabbitMQ host: {host}", rabbitMqHost);

            var factory = new ConnectionFactory() { HostName = rabbitMqHost };

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
            var properties = new BasicProperties();

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: QueueName,
                mandatory: false,
                basicProperties: properties,
                body: new ReadOnlyMemory<byte>(body)
            );

            _logger.LogDebug("Published message to queue {queue}", QueueName);
        }

        [HttpGet("deliveryplan")]
        public IActionResult GetDeliveryPlanForToday()
        {
            try
            {
                if (!System.IO.File.Exists(CsvFilePath))
                {
                    _logger.LogWarning("CSV file not found at {path}", CsvFilePath);
                    return NotFound("CSV file not found.");
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                using var reader = new StreamReader(CsvFilePath);
                using var csv = new CsvReader(reader, config);

                var records = csv.GetRecords<Ship>().ToList();

                _logger.LogInformation("Fetched {count} shipping records from CSV", records.Count);

                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read delivery plan from CSV");
                return StatusCode(500, "An error occurred while reading delivery plan.");
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
