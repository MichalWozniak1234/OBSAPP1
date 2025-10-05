using Microsoft.AspNetCore.Mvc;
using OBSAPP1.Services;

namespace OBSAPP1.Controllers
{
    public class ObsController : Controller
    {
        private readonly ObsService _obs;

        public ObsController(ObsService obs)
        {
            _obs = obs;
        }

        public IActionResult Index() => View();

        [HttpPost]
        public IActionResult Start()
        {
            _obs.Start();
            return Json(new { ok = true });
        }
    }
}
