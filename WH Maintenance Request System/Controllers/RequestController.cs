using Microsoft.AspNetCore.Mvc;
using WH_Maintenance_Request_System.Models;

public class RequestController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
    
}
