using System.Collections.Generic;
using Authenticate.Data.Entities;

namespace Authenticate.Models
{
    public class AdminDashboardViewModel
    {
        public List<Menu> Menus { get; set; } = new();
        public List<APITypes> ApiTypes { get; set; } = new();
    }
}