using Microsoft.AspNetCore.Mvc;

namespace OBSAPP1.Controllers
{
    public class ObsController : Controller
    {
        public IActionResult Index() => View();

        // Strona z instrukcją i linkiem do pobrania agenta
        public IActionResult Agent() => View();
    }
}
