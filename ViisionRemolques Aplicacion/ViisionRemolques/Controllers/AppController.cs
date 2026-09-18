using Microsoft.AspNetCore.Mvc;

namespace ViisionRemolques.Controllers
{
    [Route("app")]
    public class AppController : Controller
    {
        [HttpGet("busqueda")]
        public IActionResult Busqueda()
        {
            return View();
        }

        [HttpGet("camaras")]
        public IActionResult Camaras()
        {
            return View();
        }
    }
}
