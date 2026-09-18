using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ViisionRemolques.Controllers
{
    public class HolaController : ApiBaseController
    {
        [HttpGet]
        public IResult Vista1()
        {
            return Results.Ok(new
            {
                mensaje = "Hola Mundo!"
            });
        }
    }
}
