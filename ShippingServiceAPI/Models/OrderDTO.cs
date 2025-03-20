namespace ShippingServiceAPI.Models
{
    public class OrderDTO
    {
        public string OrderId { get; set; }
        public string CustomerName { get; set; }
        public string PickupAddress { get; set; }
        public string DeliveryAddress { get; set; }

        public DateTime DateTime { get; set; }
    }

}
