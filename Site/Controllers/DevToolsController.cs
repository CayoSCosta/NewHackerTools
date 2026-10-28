using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HackerToolbox.Controllers
{
    [Authorize]
    public class DevToolsController : Controller
    {
        [HttpGet]
        public IActionResult JsonJwtTool()
        {
            return View();
        }

        [HttpGet]
        public IActionResult HashesECripto()
        {
            return View();
        }
    }

    public class JsonRequestModel { public string Input { get; set; } }
    public class JwtRequestModel { public string Token { get; set; } }
    public class HashRequestModel { public string Input { get; set; } }
}