using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace WH_Maintenance_Request_System.Controllers
{
    public class LoginController : Controller
    {
        private readonly string _connStr;

        public LoginController(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("SRT_ConvertTxtConnection");
        }

        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        public IActionResult Login(string idEmployee, string nameEn, string section, string position, DateTime expire)
        {
            try
            {
                var role = CheckRole(idEmployee);
                //var role = "ADMIN";
                if (section == "SCM_DC Logistics" && string.IsNullOrEmpty(role))
                {
                    role = "ADMIN";
                }

                if (!string.IsNullOrEmpty(role))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, idEmployee),
                        new Claim(ClaimTypes.Name, nameEn),
                        new Claim("Position", position),
                        new Claim("Section", section),
                        new Claim(ClaimTypes.Role, role)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                    var authProperties = new AuthenticationProperties
                    {
                        ExpiresUtc = expire.ToUniversalTime(),
                        IsPersistent = true 
                    };

                    HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties).Wait();

                    CookieOptions cookieOptions = new CookieOptions
                    {
                        Expires = expire,
                        HttpOnly = true, 
                        Secure = false,   
                        SameSite = SameSiteMode.Strict,
                        Domain = "http://192.168.75.16:8522/"
                    };

                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "You do not have permission to perform this action." });
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("LoginController : Login " + ex.Message);
                return Json(new { success = false, message = "Login failed. Please contact IT." });
            }
        }

        public bool CheckPermission(string idEmployee)
        {
            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    string sql = @"
                                    SELECT COUNT(*) 
                                    FROM tbl_role 
                                    WHERE user_id = @idEmployee 
                                    AND function_name = 'DASHBOARD_OE'
                                ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@idEmployee", idEmployee);

                        int count = (int)cmd.ExecuteScalar();

                        if (count > 0)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("LoginController : CheckPermission " + ex.Message);
                return false;
            }
        }

        public string CheckRole(string idEmployee)
        {
            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    string sql = @"
                        SELECT role_name 
                        FROM tbl_role 
                        WHERE user_id = @idEmployee 
                        AND function_name = 'DASHBOARD_OE'
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@idEmployee", idEmployee);

                        var result = cmd.ExecuteScalar(); 
                        return result?.ToString() ?? "";  
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("LoginController : CheckRole " + ex.Message);
                return "";
            }
        }

        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Index", "Login");
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("LoginController : Logout " + ex.Message);
                return RedirectToAction("Index", "Login");
            }
        }


    }
}