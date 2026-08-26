using Microsoft.AspNetCore.Mvc;

namespace HackerToolbox.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult Index(int statusCode)
        {
            ViewData["StatusCode"] = statusCode;
            return View("Error");
        }
    }
}