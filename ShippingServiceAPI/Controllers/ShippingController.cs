using Microsoft.AspNetCore.Mvc;

namespace ShippingServiceAPI.Controllers
{
    public class ShippingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
