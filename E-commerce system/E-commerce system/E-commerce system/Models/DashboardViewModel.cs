using System.Collections.Generic;

namespace E_commerce_system.Models
{
    public class DashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int ActiveOrdersCount { get; set; }
        public int OpenTicketsCount { get; set; }
        public int TotalProductsCount { get; set; }
        public int TotalUsersCount { get; set; }
        public List<SupportTicket> RecentTickets { get; set; }
        public List<Order> RecentOrders { get; set; }
    }
}
