using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ViisionRemolques.Controllers
{
    public class WebhookController : ApiBaseController
    {
        [HttpGet]
        [Route("Alertas")]
        public IResult Alertas()
        {
            return Results.Ok(new
            {
                msg = "!"
            });
        }
    }
}
